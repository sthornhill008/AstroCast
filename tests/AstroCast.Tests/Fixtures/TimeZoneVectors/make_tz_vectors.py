#!/usr/bin/env python3
"""Generates tz-vectors-<version>.csv, the civil-time truth table the time-zone tests read.

The truth comes from outside AstroCast: Python's zoneinfo over the pip `tzdata` package,
with the system zoneinfo directory cleared so that only the package's data is read. It
never comes from NodaTime or from AstroCast's own conversion.

Run it in a virtual environment that holds only the pinned tzdata:

    python3 -m venv venv
    venv/bin/pip install tzdata==2026.5
    venv/bin/python make_tz_vectors.py

It refuses to run on any other IANA release. To move the app to a new tzdb release, bump
NodaTime in AstroCast.Core.csproj, set EXPECTED_IANA and EXPECTED_TZDATA below to the
release the new NodaTime embeds, install that tzdata, run this script, and commit the new
table in place of the old one.

Each row is one wall-clock reading in one zone, with the instant it denotes under the
app's rule (D17):
  unique   - the reading occurs once: that instant;
  repeated - the reading occurs twice (clocks went back): the later instant;
  skipped  - the reading never occurs (clocks went forward): the first instant after the gap.

Three sets, 13 zones each:
  grid  - local noon on the 15th of Jan, Apr, Jul and Oct, every fifth year 1890-2040 (1,612)
  edges - the midpoint of every repeated and every skipped interval, 1890-2040 (1,822)
  lmt   - local noon on the 15th of Jan, Apr, Jul and Oct of every fifth year from 1800,
          while the zone's offset is not a whole minute: local mean time (1,044)

Columns: set, zone, local (the reading), kind, expectedUtc (to the second), offsetSeconds
(the offset in force at that instant) and abbreviation (the zone record's name for it).
"""
import csv
import os
import sys
import zoneinfo
from datetime import datetime, timedelta, timezone

zoneinfo.reset_tzpath(to=[])
import tzdata  # noqa: E402  (imported after the path is cleared, so only the package is read)

EXPECTED_IANA = "2026e"
EXPECTED_TZDATA = "2026.5"

ZONES = [
    "Africa/Cairo", "America/Chicago", "America/Los_Angeles", "America/New_York", "America/Phoenix",
    "America/Vancouver", "Asia/Almaty", "Asia/Jerusalem", "Asia/Shanghai", "Asia/Tehran",
    "Europe/London", "Europe/Moscow", "Europe/Paris",
]
UTC = timezone.utc
SECOND = timedelta(seconds=1)


def offset_at(z, t_utc):
    """The offset in force at a UTC instant, and the zone record's name for it."""
    d = t_utc.astimezone(z)
    return d.utcoffset(), d.tzname()


def transitions(zname, y0, y1):
    """Every offset change in [y0-01-01, y1+1-01-01) UT, located to the second by bisection."""
    z = zoneinfo.ZoneInfo(zname)
    out = []
    t = datetime(y0, 1, 1, tzinfo=UTC) - timedelta(days=2)
    end = datetime(y1 + 1, 1, 1, tzinfo=UTC) + timedelta(days=2)
    step = timedelta(hours=3)
    o_prev = offset_at(z, t)[0]
    while t < end:
        t2 = t + step
        o2 = offset_at(z, t2)[0]
        if o2 != o_prev:
            lo, hi = t, t2
            while (hi - lo) > SECOND:
                mid = (lo + (hi - lo) / 2).replace(microsecond=0)
                if mid <= lo:
                    mid = lo + SECOND
                if offset_at(z, mid)[0] == o_prev:
                    lo = mid
                else:
                    hi = mid
            # hi is the transition: the first second with the new offset
            assert offset_at(z, hi - SECOND)[0] == o_prev and offset_at(z, hi)[0] == o2, (zname, hi)
            out.append((hi, o_prev, o2))
        o_prev = o2
        t = t2
    return out


def iso(dt):
    """A naive wall-clock reading or a UTC instant, to the second."""
    return dt.strftime("%Y-%m-%dT%H:%M:%S")


def d17(z, wall):
    """The instant a reading denotes under D17, worked out from first principles."""
    ua = wall.replace(tzinfo=z, fold=0).astimezone(UTC)
    ub = wall.replace(tzinfo=z, fold=1).astimezone(UTC)
    if ua == ub:
        return "unique", ua
    back_a = ua.astimezone(z).replace(tzinfo=None)
    back_b = ub.astimezone(z).replace(tzinfo=None)
    if back_a == wall and back_b == wall:
        return "repeated", max(ua, ub)
    # Skipped: neither instant reads back as the reading; the transition lies between them.
    lo, hi = min(ua, ub), max(ua, ub)
    o_lo = offset_at(z, lo)[0]
    while (hi - lo) > SECOND:
        mid = (lo + (hi - lo) / 2).replace(microsecond=0)
        if mid <= lo:
            mid = lo + SECOND
        if offset_at(z, mid)[0] == o_lo:
            lo = mid
        else:
            hi = mid
    return "skipped", hi


def row(set_name, zname, wall):
    z = zoneinfo.ZoneInfo(zname)
    kind, instant = d17(z, wall)
    offset, name = offset_at(z, instant)
    return [set_name, zname, iso(wall), kind, iso(instant.replace(tzinfo=None)),
            int(offset.total_seconds()), name], kind


def main():
    if tzdata.IANA_VERSION != EXPECTED_IANA or tzdata.__version__ != EXPECTED_TZDATA:
        sys.exit(f"tzdata {tzdata.__version__} (IANA {tzdata.IANA_VERSION}) is installed; "
                 f"this table is pinned to tzdata {EXPECTED_TZDATA} (IANA {EXPECTED_IANA}).")

    grid, edges, lmt = [], [], []

    for zname in ZONES:
        for y in range(1890, 2041, 5):
            for m in (1, 4, 7, 10):
                grid.append(row("grid", zname, datetime(y, m, 15, 12, 0, 0))[0])

    for zname in ZONES:
        for instant, before, after in transitions(zname, 1890, 2040):
            w0 = (instant + min(before, after)).replace(tzinfo=None)
            w1 = (instant + max(before, after)).replace(tzinfo=None)
            mid = (w0 + (w1 - w0) / 2).replace(microsecond=0)
            expected = "repeated" if after < before else "skipped"
            r, kind = row("edges", zname, mid)
            assert kind == expected, (zname, iso(mid), kind, expected)
            edges.append(r)

    for zname in ZONES:
        z = zoneinfo.ZoneInfo(zname)
        last = None
        for y in range(1800, 1990):
            for m in range(1, 13):
                if datetime(y, m, 15, 12, tzinfo=z).utcoffset().total_seconds() % 60 != 0:
                    last = y
        if last is None:
            continue
        for y in range(1800, last + 1, 5):
            for m in (1, 4, 7, 10):
                lmt.append(row("lmt", zname, datetime(y, m, 15, 12, 0, 0))[0])

    assert (len(grid), len(edges), len(lmt)) == (1612, 1822, 1044), (len(grid), len(edges), len(lmt))

    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), f"tz-vectors-{EXPECTED_IANA}.csv")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(f"# IANA tzdb {EXPECTED_IANA}: Python zoneinfo over tzdata {EXPECTED_TZDATA}, "
                f"written by make_tz_vectors.py. D17: repeated -> the later instant, "
                f"skipped -> the first instant after the gap.\n")
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["set", "zone", "local", "kind", "expectedUtc", "offsetSeconds", "abbreviation"])
        w.writerows(grid + edges + lmt)
    print(f"{path}: grid {len(grid)}, edges {len(edges)}, lmt {len(lmt)}")


if __name__ == "__main__":
    main()
