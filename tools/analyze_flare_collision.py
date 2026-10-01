from pathlib import Path
import pefile, capstone, hashlib, json, re
DLL=Path(r'D:\Projects\UmaTools\UmaViewer-master5\tmp\target-gameassembly-unpacked.dll')
OUT=Path('tmp/flare-collision-static-20260925.txt')
RVAS={
 'TryGetCharacterFlareCollisionPaths':0x1a68610,
 'TryGetMasterDressData':0x1a68bd0,
 'TryResolveNormalHeadModel':0x1a692a0,
 'TryReadMasterInt':0x1a691a0,
 'TryGetAssetEntryByOfficialPath':0x1a683b0,
 'FormatA_19ba670':0x19ba670,
 'FormatB_19bac20':0x19bac20,
 'FormatC_19bb080':0x19bb080,
 'GetLivePreloadEntries':0x1a5be60,
 'InitializeCharacterFlareCollision':0x1a5d460,
 'AddCharacterFlareCollision':0x1a59100,
}
expected='E273B4F499E4449BBABC361C03C91F260178BF3E687825AA3ABE6E686664569B'
actual=hashlib.sha256(DLL.read_bytes()).hexdigest().upper()
if actual!=expected: raise SystemExit(f'hash mismatch: {actual}')
pe=pefile.PE(str(DLL), fast_load=True)
base=pe.OPTIONAL_HEADER.ImageBase
md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_64); md.detail=True

def va_from_rip(ins):
    if ins.disp_size and ins.disp_size==4:
        return ins.address+ins.size+ins.disp

def rva_from_va(va): return va-base

def read_at_va(va,n=512):
    try:
        off=pe.get_offset_from_rva(rva_from_va(va)); return pe.__data__[off:off+n]
    except Exception:return b''

def text_at_va(va):
    b=read_at_va(va,512)
    for enc in ('utf-8','utf-16le'):
        try:
            s=b.decode(enc,errors='ignore').split('\x00',1)[0]
            if len(s)>=4 and sum(c.isprintable() or c in '\\/:._{}-' for c in s)/len(s)>.8:
                return enc+':'+s[:240]
        except:pass
    return ''

def fmt_target(op):
    if op.type==capstone.x86.X86_OP_IMM:return f'VA=0x{op.imm:x} RVA=0x{op.imm-base:x}'
    return ''
lines=[]
lines.append(f'DLL={DLL}')
lines.append(f'SHA256={actual}')
lines.append(f'ImageBase=0x{base:x}')
lines.append('This report is static only; no runtime behavior is inferred beyond direct instructions.')
for name,rva in RVAS.items():
    va=base+rva; off=pe.get_offset_from_rva(rva); data=pe.__data__[off:off+0x1200]
    lines.append(f'\n=== {name} RVA=0x{rva:x} VA=0x{va:x} fileoff=0x{off:x} ===')
    calls=[]; riprefs=[]; count=0; ret_seen=False
    for ins in md.disasm(data,va):
        ops=[]
        for op in ins.operands:
            if op.type==capstone.x86.X86_OP_IMM:
                ops.append(fmt_target(op))
            elif op.type==capstone.x86.X86_OP_MEM and op.mem.base==capstone.x86.X86_REG_RIP:
                t=va_from_rip(ins); riprefs.append(t)
                tx=text_at_va(t)
                ops.append(f'RIPVA=0x{t:x}'+((' '+tx) if tx else ''))
        line=f'{ins.address:016x}: {ins.mnemonic:8s} {ins.op_str}'
        if ops: line += '    ; ' + ' | '.join(ops)
        lines.append(line)
        if ins.mnemonic=='call' and ins.operands and ins.operands[0].type==capstone.x86.X86_OP_IMM:
            calls.append(ins.operands[0].imm)
        count+=1
        if ins.mnemonic=='ret':
            ret_seen=True; break
        if count>500: break
    lines.append(f'-- instructions={count} ret_seen={ret_seen}')
    lines.append('-- direct calls: '+', '.join(f'0x{x:x}(rva 0x{x-base:x})' for x in dict.fromkeys(calls)))
    lines.append('-- rip refs: '+', '.join(f'0x{x:x}(rva 0x{x-base:x})'+((' '+text_at_va(x)) if text_at_va(x) else '') for x in dict.fromkeys(riprefs)))
OUT.write_text('\n'.join(lines),encoding='utf-8')
print(OUT)
