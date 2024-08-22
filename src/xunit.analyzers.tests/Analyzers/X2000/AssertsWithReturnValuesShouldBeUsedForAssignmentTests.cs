using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Analyzers;
using Verify = CSharpVerifier<Xunit.Analyzers.AssertsWithReturnValuesShouldBeUsedForAssignment>;

public class AssertsWithReturnValuesShouldBeUsedForAssignmentTests
{

	[Fact]
	public async Task AssertSingleFollowedByAssignment_Triggers()
	{
		var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass {
	void TestMethod() {
		int item;
		List<int> list = new List<int> {1};
		Xunit.Assert.Single(list);
		item = list.Single(); 
	}
}
		";
		var expected = new[]
		{
			Verify
				.Diagnostic()
				.WithSpan(10, 3, 10, 23)
				.WithSeverity(DiagnosticSeverity.Info)
				.WithArguments("IEnumerable.Single()", Constants.Asserts.Single)
		};
		await Verify.VerifyAnalyzer(source, expected);
	}
	
	[Fact]
	public async Task AssertSingleFollowedByInitializeAndAssignment_Triggers()
	{
		var source = @"
using System.Collections.Generic;
using System.Linq;

class TestClass {
	void TestMethod() {
		int test;
		List<int> list = new List<int> {1};
		Xunit.Assert.Single(list);
		int item = list.Single(); 
	}
}
		";
		var expected = new[]
		{
			Verify
				.Diagnostic()
				.WithSpan(10, 3, 10, 23)
				.WithSeverity(DiagnosticSeverity.Info)
				.WithArguments("IEnumerable.Single()", Constants.Asserts.Single)
		};
		await Verify.VerifyAnalyzer(source, expected);
	}
}
