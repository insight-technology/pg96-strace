# straceログ取得と変換スクリプト

Postgres9.6のシステムコール発行をstraceで取得するコンテナとstraceログをviewer用に変換するPythonスクリプト

## ログ取得用コンテナ

```
docker build -t pgstrace ./
docker run -it {-e ユーザー・ログインの環境変数} -p 5432:5432 pgstrace
```

`log/trace_20221125-193949.log` のようなファイル名で出力される

## 変換
```
# Python3.9以上
python convert.py {入力straceログファイル} {出力名(.jsonl)}
```

## その他
+ 対応システムコール
  + プロセス: execve, clone, exit_group, kill
  + ファイル: open, read, write, close, unlink
  + ソケット: socket, accept, bind, connect, listen, sendto, recvfrom
  + メモリ: mmap, munmap
  + その他: pipe, epoll_create1
+ 変換スクリプトで除外したシステムコール
  + ライブラリ(soファイル)ロード
  + 失敗したコール(戻り値が-1など)
  + dup
