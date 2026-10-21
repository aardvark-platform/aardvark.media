# Notification browser regression

Set an explicit Chromium executable to run the notification regression:

```sh
export AARDVARK_CHROMIUM=/usr/bin/google-chrome
for configuration in Debug Release; do
  dotnet test src/Tests/Aardvark.UI.Tests/Aardvark.UI.Tests.fsproj \
    -c "$configuration" --filter 'FullyQualifiedName~Notifications are isolated' || exit
done
```

Linux CI runs both configurations. Without this variable the browser test is
skipped; an invalid executable, script error, timeout, or incomplete result fails.
No browser framework or npm installation is needed. The fixture renders generated
notification boot/channel output with the built assemblies' bundled resources and
captures only WebSocket transport. It does not validate live sockets or CEF.
