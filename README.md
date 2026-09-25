# Application Insights traceparent bug reproducer

This repository is a local reproducer for a traceparent propagation issue in the Microsoft Application Insights telemetry SDK.

## Expected behavior

After dependency injection configures the HTTP client, every health request should include a W3C `traceparent` header. Both servers print the received value.

## Reproduce

Run `frameworkApp-Sdk.exe` without arguments. It starts `server 1` on port 8017 and `server 2` on port 8018, makes one health call to `server 1` before DI, then configures DI and makes three calls to each server at five-second intervals. After the third cycle, the interval changes to two minutes. This delay is intentional: it allows the problematic server connection to reach its idle timeout. Once that connection is closed rather than reused, the traceparent issue disappears. Server output is labeled with its server identifier.

    frameworkApp-Sdk.exe

Use the control case to skip the pre-DI call and see that the traceparent apparing from the start on both servers, instead of only the second:

    frameworkApp-Sdk.exe --client --skip-initial-call

