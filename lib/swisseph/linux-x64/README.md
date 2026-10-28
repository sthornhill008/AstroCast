# Linux Swiss Ephemeris Library

Place libswe.so here after compiling on Linux.

Compile command (from lib/swisseph/src/):

```bash
gcc -shared -fPIC -O2 -o ../linux-x64/libswe.so \
    swecl.c swedate.c swehel.c swehouse.c swejpl.c swemmoon.c \
    swemplan.c sweph.c swephlib.c -lm
```
