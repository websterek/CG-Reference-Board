.PHONY: build-all linux windows clean

build-all: linux windows

linux:
	dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/linux

windows:
	dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/windows

clean:
	rm -rf ./dist
	rm -rf ./bin
	rm -rf ./obj
