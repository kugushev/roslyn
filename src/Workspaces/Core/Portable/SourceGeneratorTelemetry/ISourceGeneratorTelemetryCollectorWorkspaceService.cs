﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.SourceGeneratorTelemetry;

public interface ISourceGeneratorTelemetryCollectorWorkspaceService : IWorkspaceService
{
    void CollectRunResult(GeneratorDriverRunResult driverRunResult, GeneratorDriverTimingInfo driverTimingInfo, Func<ISourceGenerator, AnalyzerReference> getAnalyzerReference);
}
