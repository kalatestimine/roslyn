﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    /// <summary>
    /// Diagnostic update source for reporting workspace host specific diagnostics,
    /// which may not be related to any given project/document in the solution.
    /// For example, these include diagnostics generated for exceptions from third party analyzers.
    /// </summary>
    internal abstract class AbstractHostDiagnosticUpdateSource : IDiagnosticUpdateSource
    {
        private static ImmutableDictionary<DiagnosticAnalyzer, ImmutableHashSet<DiagnosticData>> s_analyzerHostDiagnosticsMap =
            ImmutableDictionary<DiagnosticAnalyzer, ImmutableHashSet<DiagnosticData>>.Empty;

        internal abstract Workspace Workspace { get; }

        public bool SupportGetDiagnostics
        {
            get
            {
                return false;
            }
        }

        public ImmutableArray<DiagnosticData> GetDiagnostics(Workspace workspace, ProjectId projectId, DocumentId documentId, object id, CancellationToken cancellationToken)
        {
            return ImmutableArray<DiagnosticData>.Empty;
        }

        public event EventHandler<DiagnosticsUpdatedArgs> DiagnosticsUpdated;

        protected void RaiseDiagnosticsUpdated(DiagnosticsUpdatedArgs args)
        {
            var updated = this.DiagnosticsUpdated;
            if (updated != null)
            {
                updated(this, args);
            }
        }

        internal void ReportAnalyzerDiagnostic(DiagnosticAnalyzer analyzer, Diagnostic diagnostic, Workspace workspace, Project project)
        {
            if (workspace != this.Workspace)
            {
                return;
            }

            bool raiseDiagnosticsUpdated = true;
            var diagnosticData = project != null ?
                DiagnosticData.Create(project, diagnostic) :
                DiagnosticData.Create(this.Workspace, diagnostic);

            var dxs = ImmutableInterlocked.AddOrUpdate(ref s_analyzerHostDiagnosticsMap,
                analyzer,
                ImmutableHashSet.Create(diagnosticData),
                (a, existing) =>
                {
                    var newDiags = existing.Add(diagnosticData);
                    raiseDiagnosticsUpdated = newDiags.Count > existing.Count;
                    return newDiags;
                });

            if (raiseDiagnosticsUpdated)
            {
                RaiseDiagnosticsUpdated(MakeArgs(analyzer, dxs, project));
            }
        }

        public void ClearAnalyzerReferenceDiagnostics(AnalyzerFileReference analyzerReference, string language, ProjectId projectId)
        {
            foreach (var analyzer in analyzerReference.GetAnalyzers(language))
            {
                ClearAnalyzerDiagnostics(analyzer, projectId);
            }
        }

        private void ClearAnalyzerDiagnostics(DiagnosticAnalyzer analyzer, ProjectId projectId)
        {
            ImmutableHashSet<DiagnosticData> existing;
            if (!s_analyzerHostDiagnosticsMap.TryGetValue(analyzer, out existing))
            {
                return;
            }

            // Check if analyzer is shared by analyzer references from different projects.
            var sharedAnalyzer = existing.Contains(d => d.ProjectId != null && d.ProjectId != projectId);
            if (sharedAnalyzer)
            {
                var newDiags = existing.Where(d => d.ProjectId != projectId).ToImmutableHashSet();
                if (newDiags.Count < existing.Count &&
                    ImmutableInterlocked.TryUpdate(ref s_analyzerHostDiagnosticsMap, analyzer, newDiags, existing))
                {
                    var project = this.Workspace.CurrentSolution.GetProject(projectId);
                    RaiseDiagnosticsUpdated(MakeArgs(analyzer, ImmutableHashSet<DiagnosticData>.Empty, project));
                }
            }
            else if (ImmutableInterlocked.TryRemove(ref s_analyzerHostDiagnosticsMap, analyzer, out existing))
            {
                var project = this.Workspace.CurrentSolution.GetProject(projectId);
                RaiseDiagnosticsUpdated(MakeArgs(analyzer, ImmutableHashSet<DiagnosticData>.Empty, project));

                if (existing.Any(d => d.ProjectId == null))
                {
                    RaiseDiagnosticsUpdated(MakeArgs(analyzer, ImmutableHashSet<DiagnosticData>.Empty, project: null));
                }
            }
        }

        private DiagnosticsUpdatedArgs MakeArgs(DiagnosticAnalyzer analyzer, ImmutableHashSet<DiagnosticData> items, Project project)
        {
            var id = analyzer.GetUniqueId();

            return new DiagnosticsUpdatedArgs(
                id: Tuple.Create(this, id, project?.Id),
                workspace: this.Workspace,
                solution: project?.Solution,
                projectId: project?.Id,
                documentId: null,
                diagnostics: items.ToImmutableArray());
        }

        internal ImmutableHashSet<DiagnosticData> TestOnly_GetReportedDiagnostics(DiagnosticAnalyzer analyzer)
        {
            ImmutableHashSet<DiagnosticData> diagnostics;
            if (!s_analyzerHostDiagnosticsMap.TryGetValue(analyzer, out diagnostics))
            {
                diagnostics = ImmutableHashSet<DiagnosticData>.Empty;
            }

            return diagnostics;
        }
    }
}
