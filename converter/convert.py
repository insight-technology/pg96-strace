from typing import Any, Optional, Union
import copy
import json
import re
from functools import lru_cache
from enum import Enum, auto
import sys


class SFile:
    def __init__(self, fd: int, target: str, flag: str):
        self.fd = fd
        self.target = target
        self.flag = flag
        self.r = 0
        self.w = 0

    def __repr__(self):
        return f"File: {self.fd} {self.target} {self.flag}"

    def to_dict(self):
        return {
            "class": "SFile",
            "fd": self.fd,
            "target": self.target,
            "flag": self.flag,
            "r": self.r,
            "w": self.w,
        }


class SSocket:
    def __init__(self, fd: int, domain: str, stype: str, protocol: str):
        self.fd = fd
        self.domain = domain
        self.stype = stype
        self.protocol = protocol
        self.r = 0
        self.w = 0
        self.is_out = None  # 外向き
        self.family = None
        self.bind = None
        self.target = None

    def __repr__(self):
        return f"Sock: {self.fd} {self.domain} {self.stype} {self.protocol}"

    def to_dict(self):
        return {
            "class": "SSocket",
            "fd": self.fd,
            "domain": self.domain,
            "stype": self.stype,
            "protocol": self.protocol,
            "r": self.r,
            "w": self.w,
            "is_out": self.is_out,
            "family": self.family,
            "bind": self.bind,
            "target": self.target
        }


class SStd:
    def __init__(self, fd: int):
        self.fd = fd
        self.r = 0
        self.w = 0

    def __repr__(self):
        return f"Std: {self.fd}"

    def to_dict(self):
        return {"class": "SStd", "fd": self.fd, "r": self.r, "w": self.w}


class SEpoll:
    def __init__(self, fd: int):
        self.fd = fd

    def __repr__(self) -> str:
        return f"EPoll: {self.fd}"

    def to_dict(self):
        return {"class": "SEpoll", "fd": self.fd, "r": 0, "w": 0}


class SPipe:
    def __init__(self, fd: int):
        self.fd = fd
        self.r = 0
        self.w = 0

    def __repr__(self) -> str:
        return f"SPipe: {self.fd}"

    def to_dict(self):
        return {"class": "SPipe", "fd": self.fd, "r": self.r, "w": self.w}


FdType = Union[SFile, SSocket, SStd, SEpoll, SPipe]


def parse_sock_opt(opt: str) -> Optional[Any]:
    # 対応opt
    # {sa_family=AF_UNIX, sun_path="/var/run/postgresql/.s.PGSQL.5432"}
    # {sa_family=AF_INET, sin_port=htons(5432), sin_addr=inet_addr("0.0.0.0")}, 16) = 0
    # {sa_family=AF_INET6, sin6_port=htons(5432), inet_pton(AF_INET6, "::", &sin6_addr), sin6_flowinfo=htonl(0), sin6_scope_id=0}
    opt_split = opt[1:-1].split(",")
    if opt_split[0] == "sa_family=AF_UNIX":
        return ("AF_UNIX", opt_split[1].split("=")[1][1:-1])
    elif opt_split[0] == "sa_family=AF_INET":
        return ("AF_INET", opt_split[2].split('"')[1] + "," + opt_split[1].split("(")[1][0:-1])
    elif opt_split[0] == "sa_family=AF_INET6":
        return ("AF_INET6", opt_split[3].strip()[1:-1] + "," + opt_split[1].split("(")[1][0:-1])
    return None

class SProcess:
    def __init__(self, ppid: int, pid: int, name):
        """
        parameters
        ----------
        ppid:
            親プロセスID ルートプロセスの場合は0
        pid:
            プロセスID
        name:
            名前
        """
        self.ppid = ppid
        self.pid = pid
        self.name = name
        self.fd_table: "dict[int, FdType]" = {}
        self.mmap: "dict[str, int]" = {}
        self.memory = 0

    def __repr__(self):
        return f"ppid:{self.ppid} pid:{self.pid} name:{self.name} fd:{self.fd_table}"

    def open_fd(self, f: FdType):
        self.fd_table[f.fd] = f

    def close_fd(self, fd: int):
        if fd in self.fd_table:
            del self.fd_table[fd]
        else:
            print(f"Not found file descriptor fd={fd}")

    def manip_mem(self, addr: str, amount: int) -> bool:
        if amount > 0:
            self.mmap[addr] = amount
            self.memory += amount
            return True
        else:
            if addr in self.mmap:
                del self.mmap[addr]
                self.memory += amount
                return True
        return False

    def bind_sock(self, fd: int, opt: str) -> Optional[SSocket]:
        if fd in self.fd_table:
            s_ref: SSocket = self.fd_table[fd]

            ret = parse_sock_opt(opt)
            if ret:
                s_ref.family = ret[0]
                s_ref.bind = ret[1]
                return s_ref

        return None

    def connect_sock(self, fd: int, opt: str) -> Optional[SSocket]:
        if fd in self.fd_table:
            s_ref: SSocket = self.fd_table[fd]

            ret = parse_sock_opt(opt)
            if ret:
                s_ref.family = ret[0]
                s_ref.target = ret[1]
                return s_ref

        return None

    def listen_sock(self, fd: int) -> bool:
        if fd in self.fd_table:
            self.fd_table[fd].is_out = False
            return True
        return False

    def to_dict(self):
        return {
            "ppid": self.ppid,
            "pid": self.pid,
            "name": self.name,
            "fd_table": {k: v.to_dict() for k, v in self.fd_table.items()},
            "memory": self.memory,
        }


class ContextRecorder:
    def __init__(self, fname):
        self.p_table: "dict[int, SProcess]" = {}
        self.f = open(fname, "w")

    def __del__(self):
        self.f.close()

    def __repr__(self) -> str:
        return f"{self.p_table}"

    def add_process(self, ppid, pid, name, time_part):
        self.p_table[pid] = SProcess(ppid, pid, name)

        self.p_table[pid].open_fd(SStd(0))  # stdin
        self.p_table[pid].open_fd(SStd(1))  # stdout
        self.p_table[pid].open_fd(SStd(2))  # stderr

        self.write(time_part, True, {"name": "add_proc", "pid": pid, "ppid": ppid})

    def clone_process(self, ppid, pid, name, time_part):
        if not ppid in self.p_table:
            return
        p = copy.deepcopy(self.p_table[ppid])
        p.ppid = ppid
        p.pid = pid
        if name is not None:
            p.name = name

        self.p_table[pid] = p

        self.write(time_part, True, {"name": "add_proc", "pid": pid, "ppid": ppid})

    def close_process(self, pid, time_part):
        if pid in self.p_table:
            del self.p_table[pid]
            self.write(time_part, True, {"name": "close_proc", "pid": pid})

    def open_fd(self, pid: int, fdType: FdType, time_part):
        if pid in self.p_table:
            self.p_table[pid].open_fd(fdType)
            self.write(
                time_part, True, {"name": "open_fd", "pid": pid, "fd": fdType.fd}
            )

    def close_fd(self, pid: int, fd: int, time_part):
        if pid in self.p_table:
            if fd in self.p_table[pid].fd_table:  # TODO
                self.p_table[pid].close_fd(fd)
                self.write(time_part, True, {"name": "close_fd", "pid": pid, "fd": fd})

    def read_fd(self, pid: int, fd: int, len: int, content, time_part):
        if pid in self.p_table and fd in self.p_table[pid].fd_table:
            self.p_table[pid].fd_table[fd].r += len
            self.write(
                time_part,
                False,
                {
                    "name": "read_fd",
                    "pid": pid,
                    "fd": fd,
                    "content": content,
                    "len": len,
                },
            )

    def write_fd(self, pid: int, fd: int, len: int, content, time_part):
        if pid in self.p_table and fd in self.p_table[pid].fd_table:
            self.p_table[pid].fd_table[fd].w += len
            self.write(
                time_part,
                False,
                {
                    "name": "write_fd",
                    "pid": pid,
                    "fd": fd,
                    "content": content,
                    "len": len,
                },
            )

    def accept_sock(self, pid, srcFd: int, fd: int, time_part):
        if pid in self.p_table and srcFd in self.p_table[pid].fd_table:
            s: SSocket = copy.deepcopy(self.p_table[pid].fd_table[srcFd])
            s.fd = fd
            s.is_out = False
            s.r = 0
            s.w = 0
            self.p_table[pid].open_fd(s)
            self.write(
                time_part,
                True,
                {"name": "accept", "pid": pid, "src": srcFd, "fd": s.fd},
            )

    def bind_sock(self, pid, fd: int, opt: str, time_part):
        if pid in self.p_table:
            s_ref: SSocket = self.p_table[pid].bind_sock(fd, opt)
            if s_ref:
                self.write(
                    time_part,
                    False,
                    {
                        "name": "bind",
                        "pid": pid,
                        "fd": fd,
                        "family": s_ref.family,
                        "bind": s_ref.bind,
                    },
                )

    def connect_sock(self, pid, fd: int, opt: str, time_part):
        if pid in self.p_table:
            s_ref: SSocket = self.p_table[pid].connect_sock(fd, opt)
            if s_ref:
                self.write(
                    time_part,
                    False,
                    {
                        "name": "connect",
                        "pid": pid,
                        "fd": fd,
                        "family": s_ref.family,
                        "target": s_ref.target,
                    },
                )

    def listen_sock(self, pid: int, fd: int, time_part):
        if pid in self.p_table:
            if self.p_table[pid].listen_sock(fd):
                self.write(
                    time_part,
                    False,
                    {"name": "listen", "pid": pid, "fd": fd}
                )

    def manip_mem(self, pid: int, addr: str, amount: int, time_part):
        if pid in self.p_table:
            if self.p_table[pid].manip_mem(addr, amount):
                self.write(
                    time_part,
                    False,
                    {"name": "manip_mem", "pid": pid, "addr": addr, "amount": amount},
                )

    def send_signal(self, pid: int, to: int, act: str, time_part):
        if pid in self.p_table and to in self.p_table:
            self.write(
                time_part,
                False,
                {"name": "send_signal", "pid": pid, "to": to, "act": act},
            )

    def write(self, time_part: str, with_tree, event_data: Any):
        self.f.write(
            json.dumps(
                {
                    "time": time_part,
                    "event": event_data,
                    "p_table": {k: v.to_dict() for k, v in self.p_table.items()}
                    if with_tree
                    else None,
                }
            )
        )
        self.f.write("\n")


class Token(Enum):
    STR = auto()
    INT = auto()
    QUOTED = auto()
    ARRAY_LIKE = auto()  # [/* comment */]
    LONG_CONTENT = (
        auto()
    )  # all logged (3, "hoge", 4096) = 4 / truncated (3, "fuga"..., 4096) = 1024
    BRACE = auto()
    BRACE2 = auto()


REG_PATTERN = {
    Token.STR: "(.*)",
    Token.INT: "(-?\d+|NULL)",
    Token.QUOTED: r"(\".*\")",
    Token.ARRAY_LIKE: r"(\[.*\])",
    Token.LONG_CONTENT: r"(\".*\"\.*)",
    Token.BRACE: r"({.*})",
    Token.BRACE2: r"({.*{.*}.*})",
}


@lru_cache(None)
def make_reg(args: "list[Token]", r_val: Optional[Token] = None) -> "re.Pattern":
    pattern_repr = "\s*,\s*".join([REG_PATTERN[elm] for elm in args])
    s = rf".*\({pattern_repr}\)"
    if r_val is not None:
        s += rf"\s*=\s*{REG_PATTERN[r_val]}"
    return re.compile(s)


def parse_reg(
    s: str, args: "list[Token]", r_val: Optional[Token] = None
) -> "list[Any]":
    reg = make_reg(args, r_val)

    matched = reg.match(s)
    if matched is None:
        return None

    ret = list(matched.groups())
    for i in range(len(args)):
        if args[i] == Token.INT:
            if ret[i] == "NULL":
                ret[i] = None
            else:
                ret[i] = int(ret[i])
    if r_val is not None:
        if r_val == Token.INT:
            ret[-1] = int(ret[-1])

    return ret


def parse_execve(s: str):
    ret = parse_reg(s, (Token.QUOTED, Token.STR, Token.ARRAY_LIKE), Token.INT)
    return {
        "name": ret[0].strip('"'),
        "arg": ret[1],
        "rest_args": ret[2],
        "ret": ret[3],
    }


def parse_clone(s: str):
    ret = parse_reg(s, (Token.STR, Token.STR, Token.STR), Token.INT)
    return {
        "child_stack": ret[0],
        "flags": ret[1],
        "child_tidptr": ret[2],
        "pid": ret[3],
    }


def parse_open(s: str) -> Optional[SFile]:
    if s.find("ENOENT") != -1:
        return None
    ret = parse_reg(s, (Token.STR, Token.STR, Token.STR), Token.INT)
    if ret is not None:
        return SFile(ret[3], ret[0].strip('"'), ret[1])

    ret = parse_reg(s, (Token.STR, Token.STR), Token.INT)
    return SFile(ret[2], ret[0].strip('"'), ret[1])


def parse_read(s: str):
    ret = parse_reg(s, (Token.INT, Token.LONG_CONTENT, Token.INT), Token.INT)
    return {
        "fd": ret[0],
        "content": ret[1],
        "count": ret[2],  # length required
        "len": ret[3],  # length result
    }


def parse_write(s: str):
    ret = parse_reg(s, (Token.INT, Token.LONG_CONTENT, Token.INT), Token.INT)
    if ret is None:
        return None
    return {
        "fd": ret[0],
        "content": ret[1],
        "count": ret[2],  # length required
        "len": ret[3],  # length result
    }


def parse_close(s: str):
    ret = parse_reg(s, (Token.INT,), Token.INT)
    return {"fd": ret[0], "ret": ret[1]}


def parse_unlink(s: str):
    if s.find("ENOENT") != -1:
        return None
    ret = parse_reg(s, (Token.QUOTED,), Token.INT)
    return {"target": ret[0].strip('"')}


def parse_rename(s: str):
    ret = parse_reg(s, (Token.STR, Token.STR), Token.INT)
    return {"src": ret[0].strip('"'), "dest": ret[1].strip('"')}


def parse_socket(s: str):
    ret = parse_reg(s, (Token.STR, Token.STR, Token.STR), Token.INT)
    return SSocket(ret[3], ret[0], ret[1], ret[2])


def parse_bind(s: str):
    ret = parse_reg(s, (Token.INT, Token.BRACE, Token.INT), Token.INT)
    if ret is None:
        return None
    return {"fd": ret[0], "opt": ret[1], "ret": ret[3]}


def parse_connect(s: str):
    ret = parse_reg(s, (Token.INT, Token.BRACE, Token.INT), Token.INT)
    if ret is None:
        return None
    return {"fd": ret[0], "opt": ret[1], "ret": ret[3]}


def parse_listen(s: str):
    ret = parse_reg(s, (Token.INT, Token.INT), Token.INT)
    return {"fd": ret[0], "ret": ret[2]}


def parse_sendto(s: str):
    ret = parse_reg(
        s,
        (Token.INT, Token.LONG_CONTENT, Token.INT, Token.INT, Token.INT, Token.INT),
        Token.INT,
    )
    if ret is not None:
        return {
            "fd": ret[0],
            "content": ret[1],
            "count": ret[2],  # length required
            "len": ret[6],  # length result
        }

    ret = parse_reg(
        s,
        (Token.INT, Token.BRACE2, Token.INT, Token.INT, Token.BRACE, Token.INT),
        Token.INT,
    )
    return {
        "fd": ret[0],
        "content": ret[1],
        "count": ret[2],  # length required
        "len": ret[6],  # length result
    }


def parse_recvfrom(s: str):
    ret = parse_reg(
        s,
        (Token.INT, Token.LONG_CONTENT, Token.INT, Token.INT, Token.INT, Token.INT),
        Token.INT,
    )
    if ret is None:
        return None
    return {
        "fd": ret[0],
        "content": ret[1],
        "count": ret[2],  # length required
        "len": ret[6],  # length result
    }


def parse_pipe(s: str):
    ret = parse_reg(s, (Token.STR,), Token.INT)
    vals = ret[0][1:-1].split(",")
    return [SPipe(int(vals[0])), SPipe(int(vals[1]))]


def parse_epoll_create1(s: str):
    ret = parse_reg(s, (Token.STR,), Token.INT)
    return SEpoll(ret[1])


def parse_brk(s: str):
    ret = parse_reg(s, (Token.INT,), Token.INT)
    return {"addr": ret[0], "ret": ret[1]}


def parse_accept(s: str):
    ret = parse_reg(s, (Token.INT, Token.STR, Token.STR), Token.INT)
    return {"source": ret[0], "fd": ret[3]}


def parse_mmap(s: str):
    ret = parse_reg(
        s, (Token.STR, Token.INT, Token.STR, Token.STR, Token.INT, Token.STR), Token.STR
    )
    if ret[6].startswith("-1"):
        return None

    return {"amount": ret[1], "fd": ret[4], "addr": ret[6]}


def parse_munmap(s: str):
    ret = parse_reg(s, (Token.STR, Token.INT), Token.INT)
    return {"amount": ret[1], "addr": ret[0]}


def parse_kill(s: str):
    ret = parse_reg(s, (Token.INT, Token.STR), Token.INT)
    return {"to": ret[0], "act": ret[1], "ret": ret[2]}


def convert(src: str, dst: str):
    cr = ContextRecorder(dst)
    pending_events: "dict[int, str]" = {}

    with open(src) as f:
        for l in f:
            if len(l) == 0:
                continue

            s = l.strip()

            pid_part, time_part, cmd_part = re.split("\s+", s, 2)

            pid = int(pid_part)

            if cmd_part.endswith("<unfinished ...>"):
                pending_events[pid] = cmd_part
                continue

            if cmd_part.startswith("<..."):
                # resume
                cmd_part = (
                    pending_events.pop(pid)[:-17] + cmd_part[cmd_part.find(">") + 2 :]
                )

            # process
            if cmd_part.startswith("execve"):
                ret = parse_execve(cmd_part)
                cr.add_process(0, pid, ret["name"], time_part)
            elif cmd_part.startswith("clone"):
                ret = parse_clone(cmd_part)
                cr.clone_process(pid, ret["pid"], None, time_part)
            elif cmd_part.startswith("exit_group"):
                cr.close_process(pid, time_part)

            # file / socket
            elif cmd_part.startswith("open"):
                ret = parse_open(cmd_part)
                if not (
                    ret is None
                    or ret.target.startswith("/etc")
                    or ret.target.startswith("/lib")
                    or ret.target.startswith("/usr/lib")
                    or ret.target.startswith("/usr/share")
                    or ret.target.startswith("/proc")
                ):
                    cr.open_fd(pid, ret, time_part)
            elif cmd_part.startswith("read"):
                ret = parse_read(cmd_part)
                if ret["len"] > 0:
                    cr.read_fd(pid, ret["fd"], ret["len"], ret["content"], time_part)
            elif cmd_part.startswith("write"):
                ret = parse_write(cmd_part)
                if ret is not None and ret["len"] > 0:
                    cr.write_fd(pid, ret["fd"], ret["len"], ret["content"], time_part)
            elif cmd_part.startswith("close"):
                ret = parse_close(cmd_part)
                cr.close_fd(pid, ret["fd"], time_part)
            elif cmd_part.startswith("unlink"):
                ret = parse_unlink(cmd_part)
            elif cmd_part.startswith("socket"):
                ret = parse_socket(cmd_part)
                cr.open_fd(pid, ret, time_part)
            elif cmd_part.startswith("accept"):
                ret = parse_accept(cmd_part)
                cr.accept_sock(pid, ret["source"], ret["fd"], time_part)
            elif cmd_part.startswith("bind"):
                ret = parse_bind(cmd_part)
                if ret is not None:
                    cr.bind_sock(pid, ret["fd"], ret["opt"], time_part)
            elif cmd_part.startswith("connect"):
                ret = parse_connect(cmd_part)
                if ret is not None:
                    cr.connect_sock(pid, ret["fd"], ret["opt"], time_part)
            elif cmd_part.startswith("listen"):
                ret = parse_listen(cmd_part)
                cr.listen_sock(pid, ret["fd"], time_part)
            elif cmd_part.startswith("sendto"):
                ret = parse_sendto(cmd_part)
                if ret is not None:
                    if ret["len"] > 0:
                        cr.write_fd(
                            pid, ret["fd"], ret["len"], ret["content"], time_part
                        )
            elif cmd_part.startswith("recvfrom"):
                ret = parse_recvfrom(cmd_part)
                if ret is not None:
                    if ret["len"] > 0:
                        cr.read_fd(
                            pid, ret["fd"], ret["len"], ret["content"], time_part
                        )

            elif cmd_part.startswith("pipe"):
                ret = parse_pipe(cmd_part)
                cr.open_fd(pid, ret[0], time_part)
                cr.open_fd(pid, ret[1], time_part)

            elif cmd_part.startswith("epoll_create1"):
                ret = parse_epoll_create1(cmd_part)
                cr.open_fd(pid, ret, time_part)

            elif cmd_part.startswith("mmap"):
                ret = parse_mmap(cmd_part)
                if ret is not None and ret["fd"] == -1:
                    cr.manip_mem(pid, ret["addr"], ret["amount"], time_part)
            elif cmd_part.startswith("munmap"):
                ret = parse_munmap(cmd_part)
                cr.manip_mem(pid, ret["addr"], -ret["amount"], time_part)

            elif cmd_part.startswith("kill"):
                ret = parse_kill(cmd_part)
                cr.send_signal(pid, ret["to"], ret["act"], time_part)

            else:
                pass
                # print(f'Not supported {l}')


if __name__ == "__main__":
    src = sys.argv[1]
    dst = sys.argv[2]

    convert(src, dst)
