"""Run API tests against a disposable local server; no production credentials used."""
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def main():
    dll = ROOT / "bin/Release/net8.0/StudioCore.dll"
    if not dll.exists():
        raise RuntimeError("Build first: dotnet build StudioCore.csproj -c Release")
    with tempfile.TemporaryDirectory(prefix="may-backend-api-") as directory:
        # Do not inherit connection strings, storage configuration or .NET environment overrides.
        prefixes = ("connectionstrings__", "storage__", "data__", "googledrive__", "aspnetcore_", "dotnet_", "crm_test_")
        env = {key: value for key, value in os.environ.items()
               if not key.lower().startswith(prefixes) and key.lower() not in ("database_url", "admincode")}
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            port = sock.getsockname()[1]
        base = "http://127.0.0.1:" + str(port)
        env.update(Data__Directory=directory, Storage__Provider="local",
                   ConnectionStrings__Postgres="", DATABASE_URL="",
                   AdminCode=secrets.token_urlsafe(32), ASPNETCORE_URLS=base,
                   ASPNETCORE_ENVIRONMENT="Production", CRM_TEST_URL=base)
        env["CRM_TEST_PASSWORD"] = env["AdminCode"]
        # Keep the temporary server log private: it may contain configuration diagnostics.
        with tempfile.TemporaryFile() as log:
            process = subprocess.Popen([os.environ.get("DOTNET_BIN", "dotnet"), str(dll)], cwd=ROOT,
                                       env=env, stdout=log, stderr=log)
            try:
                deadline = time.monotonic() + 30
                while True:
                    if process.poll() is not None:
                        raise RuntimeError("Disposable server exited before readiness")
                    try:
                        with urllib.request.urlopen(base + "/api/health", timeout=1) as response:
                            if response.status == 200:
                                break
                    except (OSError, urllib.error.URLError):
                        pass
                    if time.monotonic() >= deadline:
                        raise RuntimeError("Disposable server did not become ready within 30 seconds")
                    time.sleep(0.1)
                for script in ("auth_smoke.py", "finance_smoke.py", "home_image_smoke.py"):
                    subprocess.run([sys.executable, str(ROOT / "tests" / script)], cwd=ROOT, env=env, check=True)
                subprocess.run([sys.executable, str(ROOT / "tests/storage_s3_smoke.py")], cwd=ROOT, env=env, check=True)
            finally:
                if process.poll() is None:
                    process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


if __name__ == "__main__":
    main()
