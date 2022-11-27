# straceログファイルと変換後のjsonlファイル

## short
psqlからのSQL実行のログ
```
select * from information_schema.columns limit 2;
create table t (a int, b text);
insert into t values (1, 'test');
select a, b from t;
bgin;
update t set b = 'test2' where a = 1;
select * from t;
rollback;
insert into t values (2, repeat('a', 10000000));
\dt
```

## pgbench
接続数10のpgbench実行時のログ
