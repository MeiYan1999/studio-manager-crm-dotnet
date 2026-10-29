#!/bin/zsh
cd -- "$(dirname -- "$0")" || exit 1
python3 ./start-local-review.py
review_status=$?
if (( review_status != 0 )); then
  printf '\nPreview chưa mở được. Nhấn Enter để đóng cửa sổ.\n'
  read -r
fi
exit "$review_status"
