@rem Copyright 2016 gRPC authors.
@rem
@rem Licensed under the Apache License, Version 2.0 (the "License");
@rem you may not use this file except in compliance with the License.
@rem You may obtain a copy of the License at
@rem
@rem     http://www.apache.org/licenses/LICENSE-2.0
@rem
@rem Unless required by applicable law or agreed to in writing, software
@rem distributed under the License is distributed on an "AS IS" BASIS,
@rem WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
@rem See the License for the specific language governing permissions and
@rem limitations under the License.

@rem Generate the C# code for .proto files

setlocal

@rem enter this directory
cd /d %~dp0

@rem Last generated from the following commit hash in https://github.com/census-instrumentation/opencensus-proto:
@rem bbcfad0758ee076a159f49b47f814e05cd045fca

@rem CHANGE THIS TO YOUR LOCAL ENLISTMENT OF https://github.com/census-instrumentation/opencensus-proto
set PROTODIR=D:\Git\opencensus-proto\src

@rem CHANGE THIS TO THE APPROPRIATE VERSION OF Google.Protobuf.Tools NuGet package present on your machine
set PROTOCDIR=%UserProfile%\.nuget\packages\Google.Protobuf.Tools\3.6.0\tools\
set PROTOC=%PROTOCDIR%\windows_x64\protoc.exe

@rem CHANGE THIS TO THE APPROPRIATE VERSION OF Grpc.Tools NuGet package present on your machine
set PLUGIN=%UserProfile%\.nuget\packages\Grpc.Tools\1.13.1\tools\windows_x64\grpc_csharp_plugin.exe

@echo Generating protobuf messages...
%PROTOC% -I=%PROTODIR%\opencensus\proto --proto_path=%PROTOCDIR% --proto_path=%PROTODIR% --csharp_out=..\code --csharp_opt=file_extension=.g.cs  %PROTODIR%\opencensus\proto\trace\v1\trace.proto %PROTODIR%\opencensus\proto\trace\v1\trace_config.proto %PROTODIR%\opencensus\proto\agent\common\v1\common.proto %PROTODIR%\opencensus\proto\metrics\v1\metrics.proto 

@echo Generating GRPC services...
%PROTOC% -I=%PROTODIR%\opencensus\proto --proto_path=%PROTOCDIR% --proto_path=%PROTODIR% --csharp_out=..\code --csharp_opt=file_extension=.g.cs --grpc_out=..\code --plugin=protoc-gen-grpc=%PLUGIN% %PROTODIR%\opencensus\proto\agent\trace\v1\trace_service.proto %PROTODIR%\opencensus\proto\agent\metrics\v1\metrics_service.proto

@echo Don't forget to update the commit hash when regenerating the sources and commiting them

endlocal
