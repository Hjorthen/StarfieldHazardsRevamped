starfield_data_dir = /home/sehj/.local/share/Steam/steamapps/common/Starfield/Data
patcher_binary_path = ./bin/Debug/net8.0/mutagen.dll
modfile = HaOS.esp
psc_sources := $(wildcard papyrus/*.psc)
pex_targets  := $(patsubst papyrus/%.psc,$(starfield_data_dir)/Scripts/%.pex,$(psc_sources))

.PHONY: deploy papyrus mod_install dotnet_build build

deploy: mod_install papyrus

mod_install: $(starfield_data_dir)/HaOS.esp

papyrus: $(pex_targets)

$(starfield_data_dir)/Scripts/%.pex : papyrus/%.psc
# Check if we have any scripts that are newer than our compiled ones
	@echo "$? has been updated. You must compile them again."
	@cp "$?" "$(dir $@)/Source/$(notdir $?)"
# Move the old compiled file so the script shows up as not compiled in CreationKit
	@test -f "$@" && mv "$@" "$@".bak || true

$(starfield_data_dir)/HaOS.esp: $(modfile)
	@echo "Updating modfile"
# Create a backup
	cp "$@" "$@".bak
	cp "$<" "$@"

$(modfile): $(patcher_binary_path)
	dotnet run


$(patcher_binary_path): dotnet_build\
	;

build: dotnet_build

dotnet_build:
	dotnet build