# Dictionary lookup: selected disassembly

Excerpts from the recorded Release/x64 diagnostic runs for measured source `03232fb0cf7e5dc68a83a3a4ca0b06216f1ddf9f`. Each code block is an unchanged contiguous fragment; intervening instructions are omitted between blocks. Addresses and registers are specific to these captures.

These fragments show the FindValue call and indirect comparer calls on .NET 8, and the guarded comparer specialization and hash arithmetic within ResolveInventory on newer runtimes. Indirect fallback calls remain. They do not quantify causality or identify code for each individual launch. Recreate full listings with the [diagnostic commands](../../../benchmarks/article-001/README.md#supplemental-diagnostics).

## .NET 8.0.31

```assembly
       mov       rdx,[rdi+rcx*8+10]
       mov       rcx,rbx
       call      qword ptr [6940]; System.Collections.Generic.Dictionary`2[[System.__Canon, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]].FindValue(System.__Canon)
       test      rax,rax
       je        short 000000000000F498
       mov       eax,[rax]
       jmp       short 000000000000F4AD
```

```assembly
       mov       rcx,rdi
       mov       rdx,rsi
       call      qword ptr [r11]
       mov       ebp,eax
       mov       rcx,[rbx+8]
       mov       edx,ebp
       imul      rdx,[rbx+30]
```

## .NET 10.0.12

```assembly
       je        near ptr 0000000000005F4B
       mov       r15,[rbx+18]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r15],rcx
       jne       near ptr 0000000000005FF0
       lea       rcx,[r14+0C]
       mov       [rsp+38],rcx
```

```assembly
       add       eax,0FFFFFFFC
       mov       r8d,ecx
       rol       r8d,5
       add       ecx,r8d
       xor       ecx,[r11]
       mov       r8d,edx
       rol       r8d,5
```

```assembly
       mov       rdx,r14
       mov       r11,7FFBEBCF0B60
       call      qword ptr [r11]
       mov       r13d,eax
       jmp       near ptr 0000000000005E59
M01_L22:
       mov       rcx,r15
```

## .NET 11.0.0-rc.1.26425.128

```assembly
       je        short 0000000000003B83
       mov       r15,[rbx+18]
       mov       rcx,offset MT_System.Collections.Generic.NonRandomizedStringEqualityComparer+OrdinalComparer
       cmp       [r15],rcx
       jne       near ptr 0000000000003D91
       lea       rcx,[r14+0C]
       mov       [rsp+38],rcx
```

```assembly
       add       eax,0FFFFFFFC
       mov       r8d,ecx
       rol       r8d,5
       add       ecx,r8d
       xor       ecx,[r11]
       mov       r8d,edx
       rol       r8d,5
```

```assembly
       mov       rdx,r14
       mov       r11,7FFC05E70A98
       call      qword ptr [r11]
       mov       r13d,eax
       jmp       near ptr 0000000000003C21
M01_L18:
       mov       rcx,r15
```
