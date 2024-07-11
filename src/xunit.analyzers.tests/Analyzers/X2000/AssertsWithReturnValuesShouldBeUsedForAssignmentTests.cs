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
				var collection = new List<int> {1};
				Xunit.Assert.Single(collection);
				int item = collection.Single();
			}
		}
		";
		await Verify.VerifyAnalyzer(source);
	}
}
