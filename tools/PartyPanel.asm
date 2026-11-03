pushfq
push rax
push rcx
push r10
push r11
movabs r11, 0x1122334455667788
mov eax, 1
mov ecx, 2
lock cmpxchg dword ptr [r11], ecx
jne finish
mov r10, [rdx]
mov al, [r10+0x1f4]
mov [r11+32], al
cmp al, 1
ja failed
cmp dword ptr [r11+4], 1
jne dry
mov byte ptr [r10+0x1f4], 1
mov rax, [rdx+0x10]
mov ecx, [rdx+0x1c]
mov [rax], ecx
mov dword ptr [r11], 3
jmp finish
dry:
mov dword ptr [r11], 4
jmp finish
failed:
mov dword ptr [r11], 5
finish:
pop r11
pop r10
pop rcx
pop rax
popfq
