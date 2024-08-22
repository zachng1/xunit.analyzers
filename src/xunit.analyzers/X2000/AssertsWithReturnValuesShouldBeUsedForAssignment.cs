using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Xunit.Analyzers;


// todo: rename - Assert.Single can be used for assignment. Too big of a task to do for all 

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AssertsWithReturnValuesShouldBeUsedForAssignment : AssertUsageAnalyzerBase
{
	static readonly string[] targetMethods =
	{
		Constants.Asserts.Single
	};

	// map xunit methods to the extension methods that they can replace
	private static readonly Dictionary<string, string> XunitMethodToReplacementCandidates = new()
	{
		{Constants.Asserts.Single, "System.Linq.Enumerable.Single<TSource>(System.Collections.Generic.IEnumerable<TSource>)"}
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

		var semanticModel = invocationOperation.SemanticModel;
		if (semanticModel is null)
		{
			return;
		}
		if (method.Name == Constants.Asserts.Single)
		{
			var invocationNode = invocationOperation.Syntax;
			if (invocationNode is InvocationExpressionSyntax syntax)
			{
				ProcessXunitSingle(syntax, semanticModel, context, method);

			}
		}
	}

	private static void ProcessXunitSingle(InvocationExpressionSyntax invocationNode, SemanticModel semanticModel,
		OperationAnalysisContext context, IMethodSymbol method)
	{
		// clean 'steps':
		// find all references to the parameter to xunit.single (in this case 'collection').
		// find all those references in which that parameter is being used as part of a call to "System.Linq.Enumerable.Single"
		// ensure there are no references between the two which might change the collection
		//		this second can be done maybe by getting a list of all childnodes within the block
		//		then iterating over, finding those that refer to collection symbol - and if we hit any that edit the collection (calls to add, delete etc.), we return. keep going until we find the one which makes a call to Linq.Enumerable.Single() - then we exit with that as our replacement candidate.
		//		will have to do in both directions - first approaching from above -- if we don't find *any* calls to single above, then we iterate over after, looking for calls to single(). If we find one, we enter the mode of checking for changes to the collection. If we find a change, we go back to looking for calls to single(). If we find none before hitting our current node again (call to xunit.single), we start looking for calls to linq.single(). If we hit a collection changing method, we return.
		// YUK!  but that's the best algo i can think of
		
		//WORKING:
		// can find all invocation nodes that call IEnumerable.Single() within a block
		// now need to find places where the collection is written to or modified - we don't want to 
		// trigger on places that are before or after a write to the list

		var argumentToSingle = invocationNode.ArgumentList.Arguments.FirstOrDefault();
		if (argumentToSingle is null)
		{
			return;
		}
		var argumentSymbol = semanticModel.GetSymbolInfo(argumentToSingle.Expression).Symbol;
		if (argumentSymbol is null)
		{
			return;
		}

		var containingBlock = GetContainingBlock(invocationNode);
		var invocationExpressions = containingBlock.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
		
		if (invocationExpressions.Count == 0)
		{
			return;
		}

		var callsToLinqSingle = containingBlock.DescendantNodes() //here
				.OfType<InvocationExpressionSyntax>().ToList()
				.Select(x => x.Expression)
				.OfType<MemberAccessExpressionSyntax>() // to here is candidate for method
				.Where(x =>
				{ // and here
					var symbol = semanticModel.GetSymbolInfo(x).Symbol;
					if (symbol is not IMethodSymbol methodSymbol || methodSymbol.ReducedFrom is null)
					{
						return false;
					}
					return methodSymbol.ReducedFrom.ToDisplayString() ==
					       XunitMethodToReplacementCandidates[Constants.Asserts.Single]; 
				}) 
				.Where(x =>
				{
					var nodeSymbol = semanticModel.GetSymbolInfo(x.Expression).Symbol;
					if (nodeSymbol is null) return false;
					return SymbolEqualityComparer.Default.Equals(
						nodeSymbol, argumentSymbol);
				}).ToList() // to here obviously
			;
		
		if (callsToLinqSingle.Count == 0)
		{
			return;
		}

		foreach (var assignmentToCollection in callsToLinqSingle)
		{
			var symbol = semanticModel.GetSymbolInfo(assignmentToCollection).Symbol;
			if (symbol != null)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment,
						assignmentToCollection.GetLocation(),
						SymbolDisplay.ToDisplayString(
							symbol,
							SymbolDisplayFormat
								.CSharpShortErrorMessageFormat
								.WithParameterOptions(SymbolDisplayParameterOptions.None)
								.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
						),
						Constants.Asserts.Single
					)
				);
			}
		}
	}
	
	# region utility
	
	private static BlockSyntax GetContainingBlock(SyntaxNode node)
	{
		return node
			.Ancestors()
			.OfType<BlockSyntax>()
			.First();
	}
	
	# endregion

}
