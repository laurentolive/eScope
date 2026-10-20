# PC simulation example — run from the MSYS2 UCRT64 shell:
#   make            firmware DLL (GCC) + simulator host (dotnet)
#   make firmware   build/firmware.dll, build/firmware.map
#   make host       build/PcSimHost.exe
#   make clean
# MSYS2 packages: mingw-w64-ucrt-x86_64-gcc make ; host: .NET 8 SDK (dotnet in PATH)

CC        = gcc
# MSYS2 shell: Windows PATH not inherited by default, dotnet looked for at its default location
DOTNET   ?= $(shell command -v dotnet 2>/dev/null || echo "/c/Program Files/dotnet/dotnet.exe")
TE        = ..
OUT       = build
# link base of the DLL, given to the linker and to the code (SIM_LINK_BASE: relocation in eScopeCfg.h)
LINK_BASE = 0x180000000

SRC     = firmware/app.c firmware/simHw.c $(TE)/eScopeProtocol/eScopeProtocolV3.c
HDR     = $(wildcard firmware/*.h) $(wildcard $(TE)/eScopeProtocol/*.h) $(wildcard $(TE)/eScopeTransport/*.h)
CFLAGS  = -std=c99 -Wall -Wextra -O1 -g -fdata-sections \
          -Ifirmware -I$(TE)/eScopeProtocol -I$(TE)/eScopeTransport -DSIM_LINK_BASE=$(LINK_BASE)ULL
LDFLAGS = -shared -static-libgcc -Wl,--image-base=$(LINK_BASE) -Wl,-Map=$(OUT)/firmware.map

.PHONY: all firmware host clean

all: firmware host

firmware: $(OUT)/firmware.dll

# -g: DWARF for the GDB symbols of eScope; -fdata-sections: variable sizes in the map
$(OUT)/firmware.dll: $(SRC) $(HDR) | $(OUT)
	$(CC) $(CFLAGS) $(SRC) -o $@ $(LDFLAGS)

host: | $(OUT)
	"$(DOTNET)" build host -c Release -o $(OUT) --nologo -v q

$(OUT):
	mkdir -p $(OUT)

clean:
	rm -rf $(OUT) host/bin host/obj
