# Python 3 · Pack-LocalDlls.py · local private DLL bundle · Windows x64 · standard library
"""Build a deduplicated private resource from files already owned locally. No downloads."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import struct

p = argparse.ArgumentParser()
p.add_argument('--source', type=Path, required=True)
p.add_argument('--nvsmooth', type=Path, required=True)
p.add_argument('--project', type=Path, required=True)
p.add_argument('--asi-loader', type=Path, required=True)
args = p.parse_args()
out = args.project / 'private' / 'bundle'
out.mkdir(parents=True, exist_ok=True)
blocks, block_ids, files, catalog = [], {}, [], []

def add(key, data, origin):
    refs = []
    for at in range(0, len(data), 512):
        block = data[at:at+512].ljust(512, b'\0')
        if block not in block_ids:
            block_ids[block] = len(blocks)
            blocks.append(block)
        refs.append(block_ids[block])
    digest = hashlib.sha256(data).hexdigest()
    files.append(dict(Key=key, Length=len(data), Sha256=digest, Origin=origin, Refs=refs))
    return key

nvsmooth = args.nvsmooth.read_bytes()
if hashlib.sha256(nvsmooth).hexdigest() != '0bfdead0c48c00c8732a757703d27e6788c08bc71fe817d6a7630987838aa8dc':
    raise SystemExit('The previously prepared NVSmooth30 DLL does not match its recorded hash.')
add('nvsmooth-prepared.dll', nvsmooth, 'Existing locally prepared NVSmooth30 binary; ItsAdeline upstream; private test only')
for profile, label in [('RE4-D3D12', 'NVSmooth30 - D3D12 (no FPS cap)'), ('DIAGNOSTIC', 'NVSmooth30 - diagnostics (no FPS cap)')]:
    catalog.append(dict(Id=profile, Label=label, Dll='nvsmooth-prepared.dll', Target='version.dll', Config='', Profile=profile, Family='NVSmooth30'))

config = '; DLSSG for SM86 - existing local configuration, comments simplified to English.\r\n'
config += '\r\n'.join(line for line in (args.source / 'dlssg_sm86.ini').read_text(encoding='utf-8-sig').splitlines() if not line.lstrip().startswith(';')) + '\r\n'
add('dlssg_sm86.ini', config.encode('utf-8'), 'Existing local 0.3.5 INI; values preserved; comments removed')
for family, root in [('310.9', args.source), ('310.1', args.source / '310.1')]:
    for name in ['version.dll', 'winmm.dll', 'dbghelp.dll', 'dinput8.dll', 'dxgi.dll', 'd3d12.dll']:
        path = root / ('' if name == 'version.dll' else 'alternatives') / name
        key = 'sdli-' + family + '-' + name
        add(key, path.read_bytes(), 'Existing local sdli1995/dlssg_for_sm86 0.3.5 / ' + family + ' / ' + name)
        catalog.append(dict(Id=key, Label='DLSSG for SM86 ' + family + ' - ' + name, Dll=key, Target=name, Config='dlssg_sm86.ini', Profile='DLSSG', Family='DLSSG for SM86'))

# Recover the exact pre-RE4-patch image. The full expected SHA-256 is mandatory.
original = bytearray(nvsmooth)
pe = struct.unpack_from('<I', original, 0x3c)[0]
section_count = struct.unpack_from('<H', original, pe + 6)[0]
optional_size = struct.unpack_from('<H', original, pe + 20)[0]
sections = pe + 24 + optional_size
for index in range(section_count):
    at = sections + index * 40
    virtual_size, address, raw_size, raw = struct.unpack_from('<IIII', original, at + 8)
    if address <= 0x80a0 < address + max(virtual_size, raw_size):
        offset = raw + 0x80a0 - address
        if original[offset:offset+6] != bytes.fromhex('b801000000c3'):
            raise SystemExit('Prepared hook entry does not match its recorded bytes.')
        original[offset:offset+6] = bytes.fromhex('48895c240848')
        break
else:
    raise SystemExit('Hook entry RVA is not mapped.')
struct.pack_into('<I', original, pe + 24 + 64, 0)
if hashlib.sha256(original).hexdigest() != '18ed37a0f333242bb27f649c3ced3dc0cc081023a12e2bdca982b3d31aaa0fbf':
    raise SystemExit('The recovered pre-patch image does not match the recorded original hash.')
add('FramegenLab.NVSmooth30.asi', bytes(original), 'Exact pre-RE4-patch NVSmooth30 image recovered by reverting the known six-byte patch and original zero PE checksum; full original SHA-256 verified')
loader = args.asi_loader.read_bytes()
if hashlib.sha256(loader).hexdigest() != 'a1249114146c1d004335b34349df90d0714b19bbd61e671232931d930cb9168c':
    raise SystemExit('Ultimate ASI Loader v10.0.0 x64 does not match the verified official download.')
add('ultimate-asi-loader.dll', loader, 'ThirteenAG/Ultimate-ASI-Loader v10.0.0 official NoPDB x64 release; archive SHA-256 3385950d6af79257d18420b1d6904bc47480170b3dabe345f8c1339d0a49183f')
for name in ['dinput8.dll', 'xinput1_3.dll', 'winmm.dll']:
    ini = name[:-4] + '.ini'
    add(ini, b'[GlobalSets]\r\nLoadPlugins=1\r\nLoadFromScriptsOnly=1\r\nLoadRecursively=0\r\nLoadExtraPlugins=FramegenLab.NVSmooth30.asi\r\nDontLoadFromDllMain=1\r\nDebugLog=1\r\n', 'Tester ASI loader configuration: explicitly load FramegenLab.NVSmooth30.asi')
    catalog.append(dict(Id='d3d11-asi-' + name, Label='NVSmooth30 D3D11 - ASI loader (' + name + ')', Dll='ultimate-asi-loader.dll', Target=name, Config='', ExtraFiles=['FramegenLab.NVSmooth30.asi', ini], Profile='D3D11-ASI', Family='NVSmooth30-ASILoader'))

manifest = dict(Version='0.2.3-local', PrivateOnly=True, Candidates=catalog,
                Files=[{k:v for k,v in file.items() if k != 'Refs'} for file in files])
(out / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
(args.project / 'docs' / 'BUNDLED-FILES.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
with gzip.open(out / 'payload.bin.gz', 'wb', compresslevel=6) as stream:
    stream.write(b'FGTPACK2')
    stream.write(struct.pack('<ii', 512, len(blocks)))
    for block in blocks:
        stream.write(block)
    stream.write(struct.pack('<i', len(files)))
    for file in files:
        key = file['Key'].encode('utf-8')
        stream.write(struct.pack('<i', len(key)) + key)
        stream.write(struct.pack('<qi', file['Length'], len(file['Refs'])))
        stream.write(struct.pack('<' + 'i' * len(file['Refs']), *file['Refs']))
notices = args.source / 'THIRD_PARTY_NOTICES.txt'
(out / 'third-party-notices.txt').write_bytes(notices.read_bytes() + b'\n\nUltimate ASI Loader v10.0.0 - official ThirteenAG x64 build\nhttps://github.com/ThirteenAG/Ultimate-ASI-Loader\n\n' + (args.project / 'licenses' / 'Ultimate-ASI-Loader.txt').read_bytes())
print(json.dumps(dict(candidates=len(catalog), native_images=15, raw_bytes=sum(f['Length'] for f in files), unique_blocks=len(blocks), resource_bytes=(out/'payload.bin.gz').stat().st_size)))
