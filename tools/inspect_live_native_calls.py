"""Bounded native call inventory for the already recovered local viewer binary.

Never execute target code. Reads the recovered PE and API address list only.
"""
from __future__ import annotations
import argparse
import collections
import hashlib
import pathlib
import re
import capstone
import pefile

BASE = pathlib.Path(r"D:\Projects\UmaTools\UmaViewer-master5\tmp")
API = BASE / "render-pipeline-api.txt"
PE = BASE / "target-gameassembly-unpacked.dll"
ORIGINAL = pathlib.Path(r"D:\Projects\UmaTools\UmaViewer\GameAssembly.dll")
EXPECTED_ORIGINAL_SHA256 = "d678a0713257af45a6cd499854a7c8a80cb5999f5cb79e320fd12e315b28b78d"
EXPECTED_RECOVERED_SHA256 = "e273b4f499e4449bbabc361c03c91f260178bf3e687825aa3abe6e686664569b"


def verify_inputs():
    for path, expected in ((ORIGINAL, EXPECTED_ORIGINAL_SHA256),
                           (PE, EXPECTED_RECOVERED_SHA256)):
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest != expected:
            raise ValueError(f"Binary version mismatch: {path} (SHA256 {digest})")


def symbols():
    table = collections.defaultdict(list)
    for line in API.read_text(encoding="utf-8").splitlines():
        match = re.match(r"METHOD\|([^|]+)\|(.+?)\|ptr=(0x[0-9a-fA-F]+)\|rva=(0x[0-9a-fA-F]+)\|", line)
        if match:
            table[int(match[3], 16)].append(match[1] + "." + match[2])
    return table


def inspect(type_name: str, method_name: str, byte_limit: int):
    verify_inputs()
    table = symbols()
    wanted = type_name + "." + method_name
    candidates = [(address, names) for address, names in table.items() if wanted in names]
    if len(candidates) != 1:
        raise ValueError(f"Expected one method pointer for {wanted}; found {len(candidates)}")
    address = candidates[0][0]
    pe = pefile.PE(str(PE), fast_load=True)
    rva = address - pe.OPTIONAL_HEADER.ImageBase
    next_address = min((ptr for ptr in table if ptr > address), default=address + byte_limit)
    size = min(byte_limit, next_address - address)
    if size <= 0 or size > 65536:
        raise ValueError("Unreasonable function extent")
    offset = pe.get_offset_from_rva(rva)
    data = PE.read_bytes()[offset:offset + size]
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    md.detail = True
    calls = []
    branches = []
    for instruction in md.disasm(data, address):
        if instruction.mnemonic in ("call", "jmp") and len(instruction.operands) == 1:
            op = instruction.operands[0]
            if op.type == capstone.x86.X86_OP_IMM:
                destination = op.imm
                label = ";".join(table.get(destination, ("unmapped",)))
                (calls if instruction.mnemonic == "call" else branches).append(
                    {"at": hex(instruction.address), "target": hex(destination), "name": label})
    return {"method": wanted, "start": hex(address), "sizeLimit": size,
            "calls": calls, "jumps": branches}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("type")
    parser.add_argument("method")
    parser.add_argument("--bytes", type=int, default=16384)
    args = parser.parse_args()
    result = inspect(args.type, args.method, args.bytes)
    print(f"{result['method']} @ {result['start']} capped {result['sizeLimit']} bytes")
    for call in result["calls"]:
        print(f"{call['at']} -> {call['target']} {call['name']}")
    print(f"calls={len(result['calls'])} jumps={len(result['jumps'])}")


if __name__ == "__main__":
    main()
