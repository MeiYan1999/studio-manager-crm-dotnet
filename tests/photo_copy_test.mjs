import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
const source = await readFile(new URL('../wwwroot/assets/js/photo-copy.js', import.meta.url), 'utf8');
const { copyPhotoFile, describePhotoCopyError } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
function fixture(bytes, fail = '') {
  const file = new Blob([Buffer.from(bytes)]);
  let committed = null, aborted = false, chunks = [];
  const err = name => Object.assign(new Error('synthetic failure'), {name});
  const src = {getFile: async () => { if (fail === 'read') throw err('NotReadableError'); return file; }};
  const dest = {getFileHandle: async () => {
    if (fail === 'target') throw err('NotAllowedError');
    return {createWritable: async () => {
      if (fail === 'open') throw err('NotAllowedError');
      return {
        write: async data => { if (fail === 'write') throw err('QuotaExceededError'); chunks.push(Buffer.from(data)); },
        close: async () => { if (fail === 'close') throw err('NoModificationAllowedError'); committed = Buffer.concat(chunks); },
        abort: async () => { aborted = true; chunks = []; }
      };
    }};
  }};
  return {src, dest, result: () => ({committed, aborted, count:chunks.length})};
}
test('copies exact bytes in bounded chunks, including an empty file', async () => {
  const f = fixture('0123456789'); await copyPhotoFile(f.src, f.dest, 'a.jpg', 4);
  assert.equal(f.result().committed.toString(), '0123456789'); assert.equal(f.result().count, 3);
  const empty = fixture(''); await copyPhotoFile(empty.src, empty.dest, 'empty.jpg', 4);
  assert.equal(empty.result().committed.length, 0);
});
test('reports the failing stage and aborts incomplete output', async () => {
  for (const [failure, stage, type] of [['read','đọc ảnh gốc','NotReadableError'],['target','tạo tệp đích','NotAllowedError'],['open','mở quyền ghi','NotAllowedError'],['write','ghi dữ liệu ảnh','QuotaExceededError'],['close','lưu tệp đích','NoModificationAllowedError']]) {
    const f = fixture('data', failure);
    await assert.rejects(copyPhotoFile(f.src, f.dest, 'a.jpg', 4), error => {
      assert.equal(error.stage, stage); assert.equal(error.originalName, type); assert.ok(describePhotoCopyError(error).includes(type)); return true;
    });
    assert.equal(f.result().committed, null);
    assert.equal(f.result().aborted, failure === 'write' || failure === 'close');
  }
});
