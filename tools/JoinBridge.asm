pushfq
push rax
push rcx
push rdx
push rbx
push rbp
push rsi
push rdi
push r8
push r9
push r10
push r11
push r12
push r13
push r14
push r15
sub rsp, 0x1b8
movdqu [rsp+0x140], xmm0
movdqu [rsp+0x150], xmm1
movdqu [rsp+0x160], xmm2
movdqu [rsp+0x170], xmm3
movdqu [rsp+0x180], xmm4
movdqu [rsp+0x190], xmm5
movabs r12, 0x1122334455667788
mov r13, [rdx]
mov [r12+16], r13
mov rax, [r13]
mov [r12+24], rax
lock inc qword ptr [r12+8]
mov eax, 1
mov ecx, 2
lock cmpxchg dword ptr [r12], ecx
jne finish
xor r15d, r15d
xor r14d, r14d
movabs rax, 0x2233445566778899
call rax
mov ecx, [r12+40]
mov edx, 1
movabs rax, 0x33445566778899aa
call rax
test rax, rax
jz failed
mov r15, rax
mov rdi, rax
lea rsi, [r12+64]
mov ecx, [r12+40]
rep movsb
mov r14d, 1
cmp dword ptr [r12+44], 0
je command
movabs rax, 0x2233445566778899
call rax
mov ecx, [r12+44]
mov edx, 1
movabs rax, 0x33445566778899aa
call rax
test rax, rax
jz failed
mov r14, rax
mov rdi, rax
lea rsi, [r12+80]
mov ecx, [r12+44]
rep movsb
command:
lea rdi, [rsp+0x40]
xor eax, eax
mov ecx, 29
rep stosq
mov dword ptr [rsp+0x40], 24
mov eax, [r12+40]
mov [rsp+0x48], rax
mov [rsp+0x50], r15
mov [rsp+0x58], rax
mov eax, [r12+44]
mov [rsp+0x60], rax
mov [rsp+0x68], r14
mov [rsp+0x70], rax
lea rsi, [rsp+0x40]
lea rdi, [r12+256]
mov ecx, 29
rep movsq
cmp dword ptr [r12+4], 1
jne dry
mov rcx, r13
lea rdx, [rsp+0x40]
movabs rax, 0x445566778899aabb
call rax
mov dword ptr [r12], 3
jmp finish
dry:
call free_strings
mov dword ptr [r12], 4
jmp finish
failed:
call free_strings
mov dword ptr [r12], 5
jmp finish
free_strings:
sub rsp, 0x28
test r15, r15
jz free_pass
mov rcx, r15
mov edx, [r12+40]
mov r8d, 1
movabs rax, 0x5566778899aabbcc
call rax
free_pass:
cmp r14, 1
jbe free_done
mov rcx, r14
mov edx, [r12+44]
mov r8d, 1
movabs rax, 0x5566778899aabbcc
call rax
free_done:
add rsp, 0x28
ret
finish:
movdqu xmm0, [rsp+0x140]
movdqu xmm1, [rsp+0x150]
movdqu xmm2, [rsp+0x160]
movdqu xmm3, [rsp+0x170]
movdqu xmm4, [rsp+0x180]
movdqu xmm5, [rsp+0x190]
add rsp, 0x1b8
pop r15
pop r14
pop r13
pop r12
pop r11
pop r10
pop r9
pop r8
pop rdi
pop rsi
pop rbp
pop rbx
pop rdx
pop rcx
pop rax
popfq
