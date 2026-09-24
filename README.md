# Application Insights traceparent bug reproducer

This repository is a local reproducer for a traceparent propagation issue in the Microsoft Application Insights telemetry SDK. It configures the public Application Insights SDK and dependency collector directly; no internal telemetry package is required.

## Expected behavior

After dependency injection configures the HTTP client, every health request should include a W3C `traceparent` header. Both servers print the received value.

## Reproduce

Run `frameworkApp-Sdk.exe` without arguments. It starts `server 1` on port 8017 and `server 2` on port 8018, makes one health call to `server 1` before DI, then configures DI and makes three calls to each server at five-second intervals. After the third cycle, the interval changes to two minutes. This delay is intentional: it allows the problematic server connection to reach its idle timeout. Once that connection is closed rather than reused, the traceparent issue disappears. Server output is labeled with its server identifier.

    frameworkApp-Sdk.exe

Use the control case to skip the pre-DI call:

    frameworkApp-Sdk.exe --client --skip-initial-call

A separately started server can be targeted with:

    frameworkApp-Sdk.exe --server --port 9001
    frameworkApp-Sdk.exe --client --endpoint1 http://localhost:9001/healthprobe --endpoint2 http://localhost:8018/healthprobe

The Application Insights connection string and dependency tracking options are hardcoded in the SDK registration so the repro has no configuration pass-through.
