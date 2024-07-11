using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Xunit.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AssertsWithReturnValuesShouldBeUsedForAssignment : AssertUsageAnalyzerBase
{
	static readonly string[] targetMethods =
	{
		Constants.Asserts.Collection,
		Constants.Asserts.Single
	};
	
	public AssertsWithReturnValuesShouldBeUsedForAssignment() : base(Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment, targetMethods)
	{
	}

	protected override void AnalyzeInvocation(OperationAnalysisContext context, XunitContext xunitContext,
		IInvocationOperation invocationOperation, IMethodSymbol method)
	{
		Guard.ArgumentNotNull(xunitContext);
		Guard.ArgumentNotNull(invocationOperation);
		Guard.ArgumentNotNull(method);

		if (method.Name != Constants.Asserts.Single)
		{
			return;
		}

		var semanticModel = invocationOperation.SemanticModel;
		if (semanticModel is null)
		{
			return;
		}
		var node = invocationOperation.Syntax;

		
		var objectBeingAssertedOn = method.Parameters[0]; // will have to make this a sensible value for things that take more than 1 param

		var symbolUsages = semanticModel.LookupSymbols(node.Span.End, null, objectBeingAssertedOn.Name); // search for all uses of (in this case a collection) within this node

		
		context.ReportDiagnostic(
			Diagnostic.Create(
				Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment,
				invocationOperation.Syntax.GetLocation(),
				SymbolDisplay.ToDisplayString(
					method, 
					SymbolDisplayFormat
						.CSharpShortErrorMessageFormat
						.WithParameterOptions(SymbolDisplayParameterOptions.None)
						.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
					)
				)
			);
		
	}
}
