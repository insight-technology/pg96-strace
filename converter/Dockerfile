FROM postgres:9.6

# ツール
# apt.postgres.orgにstrechが保持されなくなったため削除
RUN rm /etc/apt/sources.list.d/pgdg.list \
    && apt update \
    && apt install strace

# postgres起動時にstraceを挟むように書き換える
RUN sed -i -e 's/exec "\$@"/strace -D -s 128 -tt -f -q -e "trace=!getpid,clock_gettime,gettimeofday,setitimer,semget,semop,set_robust_list,setsid,getrandom,rt_sigaction,rt_sigprocmask,rt_sigreturn,semctl,epoll_ctl,epoll_wait,fcntl,fstat,stat,mprotect,futex,set_tid_address,arch_prctl,statfs" -o \/tmp\/strace\/trace_\$(date +%Y%m%d-%H%M%S).log "\$@"/' /usr/local/bin/docker-entrypoint.sh

# postgresユーザーでアクセス可能にする
RUN mkdir /tmp/strace && chmod 0777 /tmp/strace
