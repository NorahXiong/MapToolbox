#!/usr/bin/env python3
"""Convert a binary PCD v0.7 file so the AutoCore MapToolbox plugin colors it
by intensity (revealing lane markings) instead of only by height.

Modes:
  --mode height     Drop every field except x,y,z (point_step 12). The plugin
                    then uses z as a pseudo-intensity -> rainbow by height.
                    Good for separating obstacles from the ground.
  --mode intensity  Emit FIELDS x y z intensity (point_step 16). If the input
                    already has an `intensity` field it is preserved; otherwise
                    the intensity is derived from the luminance of an `rgb`/
                    `rgba` field (0.299R+0.587G+0.114B). The plugin then runs
                    its IntensityToColor rainbow on the real reflectivity, so
                    high-reflectance lane markings stand out from the road.

Usage:
    python3 strip_pcd_color.py <input.pcd> [output.pcd] [--mode {height,intensity}]

Default mode is `height`. Default output is <input_dir>/<stem>_<mode>.pcd.
"""
import argparse
import os
import struct
import sys


def parse_header(f):
    fields, sizes, types, counts = [], [], [], []
    width = points = 0
    while True:
        line = f.readline()
        if not line:
            raise ValueError("unexpected EOF before DATA line")
        line = line.decode("utf-8", errors="replace").rstrip("\n").rstrip("\r")
        parts = line.split()
        if not parts:
            continue
        key = parts[0]
        if key == "VERSION":
            if len(parts) > 1 and not parts[1].startswith("0.7"):
                print(f"[warn] untested PCD version: {parts[1]}", file=sys.stderr)
        elif key == "FIELDS":
            fields = parts[1:]
        elif key == "SIZE":
            sizes = [int(x) for x in parts[1:]]
        elif key == "TYPE":
            types = parts[1:]
        elif key == "COUNT":
            counts = [int(x) for x in parts[1:]]
        elif key == "WIDTH":
            width = int(parts[1])
        elif key == "POINTS":
            points = int(parts[1])
        elif key == "DATA":
            if parts[1] != "binary":
                raise ValueError(f"only DATA binary supported, got: {parts[1]}")
            break
    if points == 0:
        points = width
    if len(counts) != len(fields):
        counts = [1] * len(fields)
    offsets = []
    off = 0
    for s, c in zip(sizes, counts):
        offsets.append(off)
        off += s * c
    point_step = off
    return fields, sizes, types, counts, offsets, point_step, points, width


def build_header(points, out_fields):
    size_str = " ".join("4" for _ in out_fields)
    type_str = " ".join("F" for _ in out_fields)
    count_str = " ".join("1" for _ in out_fields)
    lines = [
        "# .PCD v0.7 - Point Cloud Data file format",
        "VERSION 0.7",
        f"FIELDS {' '.join(out_fields)}",
        f"SIZE {size_str}",
        f"TYPE {type_str}",
        f"COUNT {count_str}",
        f"WIDTH {points}",
        "HEIGHT 1",
        "VIEWPOINT 0 0 0 1 0 0 0",
        f"POINTS {points}",
        "DATA binary",
        "",
    ]
    return "\n".join(lines).encode("utf-8")


def field_span(fields, sizes, counts, offsets, name):
    idx = fields.index(name)
    return offsets[idx], sizes[idx] * counts[idx]


def convert_height(fields, sizes, counts, offsets, point_step, points, data):
    ox, sx = field_span(fields, sizes, counts, offsets, "x")
    oy, sy = field_span(fields, sizes, counts, offsets, "y")
    oz, sz = field_span(fields, sizes, counts, offsets, "z")
    contiguous = (ox, oy, oz) == (0, 4, 8) and (sx, sy, sz) == (4, 4, 4)
    if contiguous:
        chunks = [data[i:i + 12] for i in range(0, len(data), point_step)]
    else:
        chunks = [
            data[i + ox:i + ox + sx] + data[i + oy:i + oy + sy] + data[i + oz:i + oz + sz]
            for i in range(0, len(data), point_step)
        ]
    return b"".join(chunks), ["x", "y", "z"]


def convert_intensity(fields, sizes, counts, offsets, point_step, points, data):
    ox, sx = field_span(fields, sizes, counts, offsets, "x")
    oy, sy = field_span(fields, sizes, counts, offsets, "y")
    oz, sz = field_span(fields, sizes, counts, offsets, "z")
    xyz_contiguous = (ox, oy, oz) == (0, 4, 8) and (sx, sy, sz) == (4, 4, 4)

    if "intensity" in fields:
        oi, si = field_span(fields, sizes, counts, offsets, "intensity")
        print("[info] using existing `intensity` field", file=sys.stderr)
        if xyz_contiguous and si == 4:
            chunks = [data[i:i + 16] for i in range(0, len(data), point_step)]
        else:
            chunks = [
                data[i + ox:i + ox + sx] + data[i + oy:i + oy + sy]
                + data[i + oz:i + oz + sz] + data[i + oi:i + oi + si]
                for i in range(0, len(data), point_step)
            ]
        return b"".join(chunks), ["x", "y", "z", "intensity"]

    color_field = None
    for cand in ("rgb", "rgba"):
        if cand in fields:
            color_field = cand
            break
    if color_field is None:
        raise ValueError(
            "intensity mode needs an `intensity` or `rgb`/`rgba` field in the input"
        )
    roff, _ = field_span(fields, sizes, counts, offsets, color_field)
    print(f"[info] deriving intensity from luminance of `{color_field}` field",
          file=sys.stderr)

    # detect grayscale on a sample to pick the fast path
    sample_end = min(len(data), 100000 * point_step)
    is_gray = all(
        data[i + roff] == data[i + roff + 1] == data[i + roff + 2]
        for i in range(0, sample_end, point_step)
    )
    pack = struct.Struct("<f").pack
    if is_gray:
        print("[info] input is grayscale (R==G==B); using R as luminance",
              file=sys.stderr)
        lut = [pack(float(b)) for b in range(256)]
        if xyz_contiguous:
            chunks = [data[i:i + 12] + lut[data[i + roff]]
                      for i in range(0, len(data), point_step)]
        else:
            chunks = [data[i + ox:i + ox + sx] + data[i + oy:i + oy + sy]
                      + data[i + oz:i + oz + sz] + lut[data[i + roff]]
                      for i in range(0, len(data), point_step)]
    else:
        print("[info] input has color; using 0.299R+0.587G+0.114B luminance",
              file=sys.stderr)
        lut_r = [0.299 * b for b in range(256)]
        lut_g = [0.587 * b for b in range(256)]
        lut_b = [0.114 * b for b in range(256)]
        if xyz_contiguous:
            chunks = [
                data[i:i + 12] + pack(lut_r[data[i + roff]] + lut_g[data[i + roff + 1]]
                                      + lut_b[data[i + roff + 2]])
                for i in range(0, len(data), point_step)
            ]
        else:
            chunks = [
                data[i + ox:i + ox + sx] + data[i + oy:i + oy + sy]
                + data[i + oz:i + oz + sz]
                + pack(lut_r[data[i + roff]] + lut_g[data[i + roff + 1]]
                       + lut_b[data[i + roff + 2]])
                for i in range(0, len(data), point_step)
            ]
    return b"".join(chunks), ["x", "y", "z", "intensity"]


def convert(in_path, out_path, mode):
    with open(in_path, "rb") as f:
        fields, sizes, types, counts, offsets, point_step, points, width = parse_header(f)
        missing = [n for n in ("x", "y", "z") if n not in fields]
        if missing:
            raise ValueError(f"input missing required fields: {missing}")
        data = f.read(points * point_step)
        if len(data) != points * point_step:
            raise ValueError(
                f"truncated data: expected {points * point_step} bytes, got {len(data)}"
            )
        print(f"[info] input FIELDS={' '.join(fields)} point_step={point_step} "
              f"points={points}", file=sys.stderr)
        if mode == "height":
            blob, out_fields = convert_height(
                fields, sizes, counts, offsets, point_step, points, data)
        else:
            blob, out_fields = convert_intensity(
                fields, sizes, counts, offsets, point_step, points, data)

    header = build_header(points, out_fields)
    with open(out_path, "wb") as f:
        f.write(header)
        f.write(blob)

    step = len(out_fields) * 4
    if out_fields == ["x", "y", "z"]:
        idx = 8
        label = "z"
    else:
        idx = 12
        label = "intensity"
    vmin = vmax = None
    vsum = 0.0
    for i in range(0, len(blob), step):
        v = struct.unpack_from("<f", blob, i + idx)[0]
        if vmin is None or v < vmin:
            vmin = v
        if vmax is None or v > vmax:
            vmax = v
        vsum += v
    n = len(blob) // step
    return {
        "fields": " ".join(out_fields),
        "point_step": step,
        "points": n,
        "label": label,
        "min": vmin,
        "max": vmax,
        "mean": vsum / n if n else 0.0,
        "out_bytes": len(header) + len(blob),
    }


def main(argv):
    ap = argparse.ArgumentParser(description="Convert PCD for MapToolbox coloring.")
    ap.add_argument("input", help="input .pcd path")
    ap.add_argument("output", nargs="?", help="output .pcd path (optional)")
    ap.add_argument("--mode", choices=("height", "intensity"), default="height",
                    help="height: color by z; intensity: color by reflectivity (default: height)")
    args = ap.parse_args(argv)

    if not os.path.isfile(args.input):
        print(f"[error] input not found: {args.input}", file=sys.stderr)
        return 1
    if args.output:
        out_path = args.output
    else:
        d, name = os.path.split(args.input)
        stem, _ = os.path.splitext(name)
        out_path = os.path.join(d, f"{stem}_{args.mode}.pcd")

    print(f"[info] {args.input} -> {out_path} (mode={args.mode})")
    info = convert(args.input, out_path, args.mode)
    print(
        f"[done] FIELDS={info['fields']} point_step={info['point_step']} "
        f"points={info['points']} {info['label']}_range=[{info['min']:.4f}, {info['max']:.4f}] "
        f"{info['label']}_mean={info['mean']:.4f} size={info['out_bytes']} bytes"
    )
    if args.mode == "intensity":
        print("[note] reimport in Unity; lane markings (high intensity) map to "
              "yellow/white, road (low intensity) to dark -> clear lane lines.")
    else:
        print("[note] reimport in Unity; the pcd ScriptedImporter will color by height (z).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
