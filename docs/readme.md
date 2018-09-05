# Local Forwarder

## Background
Local Forwarder is an agent that collects Application Insights or OpenCensus telemetry from a variety of SDKs and routes it to the Application Insights backend.
Local Forwarder is a .NET Core application capable of running under Windows and Linux. You may also be able to run it under macOS, but that is not officially supported at this time.

## Running Local Forwarder
Local Forwarder is a .NET Core application; it's an [open source project on GitHub](https://github.com/Microsoft/ApplicationInsights-LocalForwarder/releases). There is a variaty of ways to run Local Forwarder across multiple platforms.

### Windows
#### Windows Service
The most natural way of running Local Forwarder under Windows is by installing it as a Windows Service. The release comes with a Windows Service executable (*Microsoft.LocalForwarder.WindowsServiceHost.exe*) which can be easily registered with the OS by running a script similar to the following:

Register a service and configure it to start at system boot.
```
sc create "Local Forwarder" binpath="<path>\Microsoft.LocalForwarder.WindowsServiceHost.exe" start=auto
```

Configure the service to restart automatically if it fails for any reason.
```
sc failure "Local Forwarder" reset= 432000 actions= restart/1000/restart/1000/restart/1000
```

Once the service is registered, use Windows tools to manage it.

#### Console application
For certain use cases it might be beneficial to run Local Forwarder as a console application. The release comes with a .NET Core binary (*Microsoft.LocalForwarder.ConsoleHost.dll*) which can be invoked from a console.
```batchfile
dotnet Microsoft.LocalForwarder.ConsoleHost.dll
```

### Linux
Most users will want to run Local Forwarder as a daemon. Linux systems come with a variety of solutions for service management, like Upstart, sysv, or systemd. Whatever your particular version is, you can use it to run the .NET Core assembly *Microsoft.LocalForwarder.ConsoleHost.dll* in the way which is most appropriate for your scenario.

As an example, let's create a daemon service using systemd.
* create the following service file named localforwarder.service and place it into /lib/systemd/system.
This sample assumes your user name is SAMPLE_USER and you've copied Local Forwarder binaries to /home/SAMPLE_USER/LOCALFORWARDER_DIR.
```
# localforwarder.service
# Place this file into /lib/systemd/system/
# Use 'systemctl enable localforwarder' to start the service automatically on each boot
# Use 'systemctl start localforwarder' to start immediately

[Unit]
Description=Local Forwarder service
After=network.target
StartLimitIntervalSec=0

[Service]
Type=simple
Restart=always
RestartSec=1
User=SAMPLE_USER
WorkingDirectory=/home/SAMPLE_USER/LOCALFORWARDER_DIR
ExecStart=/usr/bin/env dotnet /home/SAMPLE_USER/LOCALFORWARDER_DIR/Microsoft.LocalForwarder.ConsoleHost.dll noninteractive

[Install]
WantedBy=multi-user.target
```

* Run the following command to instruct systemd to start Local Forwarder on every boot
```
systemctl enable localforwarder
```

* Run the following command to instruct systemd to start Local Forwarder immediately
```
systemctl start localforwarder
```

* Monitor the service by inspecting **.log* files in the /home/SAMPLE_USER/LOCALFORWARDER_DIR directory.

Of course, you can also run the same executable in a console.
```batchfile
dotnet Microsoft.LocalForwarder.ConsoleHost.dll
```

### Mac
You may be able to run Local Forwarder under macOS, but that is not officially supported at this time.

//!!! TODO include self-contained options that require no .NET Core installation

### Self-hosting
Local Forwarder is also distributed as a .NET Standard NuGet package, allowing you to host it inside your own .NET application.

//!!! TODO include details for downloading NuGet

```C#
using Library;
...
Host host = new Host();

// see section below on configuring Local Forwarder
string configuration = ...;
    
host.Run(config, TimeSpan.FromSeconds(5));
...
host.Stop();
```

## Configuring Local Forwarder
* When running one of Local Forwarder's own hosts (Console Host or Windows Service Host), you will find LocalForwarder.config placed next to the binary.
* When self-hosting the Local Forwarder NuGet, the configuration of the same format must be provided in code (see section on self-hosting). For the configuration syntax, please see [LocalForwarder.config](https://github.com/Microsoft/ApplicationInsights-LocalForwarder/blob/master/src/ConsoleHost/LocalForwarder.config) in the GitHub repository. Note that configuration may change from release to release, so pay attention to which version you're using.

## Monitoring Local Forwarder
Traces are written out to the file system next to the executable that runs Local Forwarder (look for **.log* files). Local Forwarder uses a tracing library called [NLog](https://nlog-project.org/), and you can place a file with a name of *NLog.config* next to the executable to provide your own NLog configuration in place of the default one. See [NLog documentation](https://github.com/NLog/NLog/wiki/Configuration-file#configuration-file-format) for the description of the format. If no configuration file is provided (which is the default), Local Forwarder will use the default NLog configuration which can be found [here](https://github.com/Microsoft/ApplicationInsights-LocalForwarder/blob/master/src/Common/NLog.config).