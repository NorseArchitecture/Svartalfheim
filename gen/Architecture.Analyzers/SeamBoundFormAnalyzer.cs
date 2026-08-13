using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Norse.Architecture.Analyzers;

/// <summary>
/// NORSE075 — OnValidSubmit on a seam-bound form. Runs over the Razor compiler's generated C#
/// (GeneratedCodeAnalysisFlags.Analyze is the point, not an accident: *_razor.g.cs is auto-generated,
/// and the default None would blind the rule to every form on the platform). Within a generated
/// render-tree body, an EditForm whose EditContext parameter is produced by EditContextFor(...) and
/// which also carries an OnValidSubmit parameter is convicted at the OnValidSubmit call — EditForm's
/// own synchronous validation pass would run ahead of SubmitAsync's async-aware gate. Model-bound
/// scaffold forms (no EditContextFor) are deliberately outside the law until they adopt the seam.
/// Frame tracking is scoped to RenderTreeBuilder calls specifically (both by containing type, not
/// name alone) — an unrelated user method sharing a name like OpenComponent/CloseComponent must never
/// walk the frame stack, and EditContextFor is matched by exact target-method name, not a textual
/// suffix, so a differently named helper (CreateEditContextFor) can't be mistaken for the seam.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SeamBoundFormAnalyzer : DiagnosticAnalyzer
{
	static readonly ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics =
		[Diagnostics.ValidSubmitOnSeamBoundForm];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		_supportedDiagnostics;

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static start =>
		{
			if (RealmIdentity.IsExempt(start.Compilation.AssemblyName ?? ""))
				return;
			var editForm = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.Forms.EditForm");
			var renderTreeBuilder = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder");
			if (editForm is null || renderTreeBuilder is null)
				return;
			start.RegisterOperationBlockAction(block => AnalyzeBlock(block, editForm, renderTreeBuilder));
		});
	}

	static void AnalyzeBlock(OperationBlockAnalysisContext context, INamedTypeSymbol editForm, INamedTypeSymbol renderTreeBuilder)
	{
		Stack<Frame> frames = new();
		foreach (var block in context.OperationBlocks)
			foreach (var operation in block.Descendants().OfType<IInvocationOperation>())
			{
				if (!SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, renderTreeBuilder))
					continue;
				switch (operation.TargetMethod.Name)
				{
					case "OpenComponent":
						var typeArguments = operation.TargetMethod.TypeArguments;
						frames.Push(new Frame(typeArguments.Length == 1
							&& SymbolEqualityComparer.Default.Equals(typeArguments[0], editForm)));
						break;
					case "CloseComponent" when frames.Count > 0:
						Convict(context, frames.Pop());
						break;
					case "AddComponentParameter" or "AddAttribute" when frames.Count > 0:
						Record(frames.Peek(), operation);
						break;
				}
			}
	}

	static void Convict(OperationBlockAnalysisContext context, Frame frame)
	{
		if (frame is { IsEditForm: true, SeamBound: true, ValidSubmit: not null })
			context.ReportDiagnostic(Diagnostic.Create(
				Diagnostics.ValidSubmitOnSeamBoundForm, frame.ValidSubmit.GetLocation()));
	}

	static void Record(Frame frame, IInvocationOperation operation)
	{
		if (!frame.IsEditForm)
			return;
		if (Argument(operation, 1)?.Value.ConstantValue is not { HasValue: true, Value: string parameterName })
			return;
		switch (parameterName)
		{
			case "EditContext":
				frame.SeamBound = Argument(operation, 2)?.Value.DescendantsAndSelf()
					.OfType<IInvocationOperation>()
					.Any(static i => i.TargetMethod.Name == "EditContextFor") == true;
				break;
			case "OnValidSubmit":
				frame.ValidSubmit = operation.Syntax;
				break;
		}
	}

	static IArgumentOperation? Argument(IInvocationOperation operation, int ordinal) =>
		operation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == ordinal);

	sealed class Frame(bool isEditForm)
	{
		internal bool IsEditForm { get; } = isEditForm;
		internal bool SeamBound { get; set; }
		internal SyntaxNode? ValidSubmit { get; set; }
	}
}
