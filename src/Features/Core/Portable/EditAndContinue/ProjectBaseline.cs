﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.CodeAnalysis.EditAndContinue;

public sealed class ProjectBaseline(ProjectId projectId, EmitBaseline emitBaseline, int generation)
{
    public ProjectId ProjectId { get; } = projectId;
    public EmitBaseline EmitBaseline { get; } = emitBaseline;
    public int Generation { get; } = generation;
}
