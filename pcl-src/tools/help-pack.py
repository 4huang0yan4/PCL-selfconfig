#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""help-pack.py -- 把 help-src/ 下的帮助文件（重新）打包进 Resources\\Help.zip

用途
    帮助内容有两条来源：
      1. 内置：Resources\\Help.zip，被 My Project\\Resources.resx 以 ResXFileRef 嵌进 exe，
         启动时解压到临时目录。**只有这份会跟着 exe 走。**
      2. 外部：<exe 同级>\\PCL\\Help\\**.json。不随 exe 移动，且与内置同名会重复出现两条。
    两处都留会重复，所以现在只维护内置那份：内容源放 help-src/，用本脚本推入 zip。

用法
    py -3 tools\\help-pack.py            # 默认：pcl-src\\help-src -> build 的 Resources\\Help.zip
    py -3 tools\\help-pack.py --check    # 只报告，不写文件
    py -3 tools\\help-pack.py --zip <路径> --src <目录>

退出码
    0 成功 / 1 失败

依赖
    Python 3.7+，仅标准库。

注意
    只处理 .json 与 .xaml；同名条目会被覆盖（这就是"更新"的方式）。
    写完后仍需重新编译，改动才进 exe。
"""
import argparse
import os
import shutil
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
SRC_DEFAULT = os.path.join(os.path.dirname(HERE), 'help-src')
ZIP_DEFAULT = os.path.join(os.path.dirname(HERE), 'build',
                          'Plain Craft Launcher 2', 'Resources', 'Help.zip')


def collect(src):
    items = []
    for root, _dirs, files in os.walk(src):
        for f in files:
            if not f.lower().endswith(('.json', '.xaml')):
                continue
            full = os.path.join(root, f)
            rel = os.path.relpath(full, src).replace('\\', '/')
            items.append((full, rel))
    items.sort(key=lambda x: x[1])
    return items


def main():
    ap = argparse.ArgumentParser(add_help=True)
    ap.add_argument('--src', default=SRC_DEFAULT)
    ap.add_argument('--zip', dest='zip_path', default=ZIP_DEFAULT)
    ap.add_argument('--check', action='store_true')
    args = ap.parse_args()

    if not os.path.isdir(args.src):
        print('[FAIL] 找不到源目录: %s' % args.src)
        return 1
    if not os.path.isfile(args.zip_path):
        print('[FAIL] 找不到目标 zip: %s' % args.zip_path)
        return 1

    items = collect(args.src)
    if not items:
        print('[FAIL] 源目录里没有 .json / .xaml')
        return 1

    with zipfile.ZipFile(args.zip_path) as z:
        before = z.namelist()
    print('目标: %s' % args.zip_path)
    print('现有条目: %d' % len(before))
    print('源文件  : %d' % len(items))

    todo = [x for x in items if x[1] not in before]
    same = len(items) - len(todo)
    print('需新增  : %d    已存在(将覆盖): %d' % (len(todo), same))

    if args.check:
        for _f, rel in items:
            print('   %s %s' % ('+' if rel in [x[1] for x in todo] else '=', rel))
        print('[CHECK] 未写入')
        return 0

    bak_dir = os.path.join(os.path.dirname(HERE), '_deprecated', 'help-zip')
    os.makedirs(bak_dir, exist_ok=True)
    backup = os.path.join(bak_dir, 'Help.zip.bak')
    shutil.copy2(args.zip_path, backup)
    print('已备份 -> %s' % backup)

    # 追加模式无法覆盖同名条目（会产生两条同名 entry），因此整体重建
    with zipfile.ZipFile(args.zip_path) as z:
        existing = {n: z.read(n) for n in z.namelist()}

    for full, rel in items:
        with open(full, 'rb') as fh:
            existing[rel] = fh.read()

    tmp = args.zip_path + '.tmp'
    with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as z:
        for name in sorted(existing):
            z.writestr(name, existing[name])
    os.replace(tmp, args.zip_path)

    with zipfile.ZipFile(args.zip_path) as z:
        after = z.namelist()
        bad = z.testzip()
    print('写入后条目: %d' % len(after))
    print('完整性: %s' % ('正常' if bad is None else '损坏 %s' % bad))

    if len(after) != len(set(after)):
        print('[FAIL] zip 内仍有重名条目')
        return 1

    # MSBuild 不跟踪 ResXFileRef 指向的 Help.zip：它只比较 Resources.resx 与
    # obj 里已生成的 .resources 谁更新。换了 zip 却不碰 resx，编译就认为资源没变，
    # 新帮助静默进不了 exe（本仓库 2026-11-03 踩过一次）。这里统一刷新时间戳。
    resx = os.path.join(os.path.dirname(os.path.dirname(args.zip_path)),
                        'My Project', 'Resources.resx')
    if os.path.isfile(resx):
        os.utime(resx, None)
        print('已刷新时间戳: %s' % resx)
    else:
        print('[WARN] 未找到 %s，若 Help.zip 未编进 exe 请手动 touch 一下' % resx)

    print('[OK] 重新编译后生效: build.cmd -NoRestore')
    return 0


if __name__ == '__main__':
    sys.exit(main())
