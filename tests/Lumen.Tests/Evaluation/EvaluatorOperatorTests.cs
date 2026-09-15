namespace Lumen.Tests.Evaluation;

public class EvaluatorOperatorTests
{
    [Theory]
    [InlineData("!true", false)]
    [InlineData("!false", true)]
    [InlineData("!!true", true)]
    public void Eval_BangOnBool_Negates(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("!5", "unknown operator: !Int")]
    [InlineData("!\"a\"", "unknown operator: !String")]
    [InlineData("!null", "unknown operator: !Null")]
    [InlineData("-\"a\"", "unknown operator: -String")]
    [InlineData("-true", "unknown operator: -Bool")]
    public void Eval_PrefixOnWrongType_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 < 1", false)]
    [InlineData("1 > 2", false)]
    [InlineData("1 <= 1", true)]
    [InlineData("1 >= 2", false)]
    [InlineData("1.5 < 2", true)]
    [InlineData("2 > 1.5", true)]
    [InlineData("1.0 <= 1", true)]
    public void Eval_NumericComparison_ProducesBool(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("1 == 1", true)]
    [InlineData("1 == 1.0", true)]
    [InlineData("1.0 == 1", true)]
    [InlineData("1 != 1.0", false)]
    [InlineData("1 == 2", false)]
    [InlineData("\"a\" == \"a\"", true)]
    [InlineData("\"a\" != \"b\"", true)]
    [InlineData("true == true", true)]
    [InlineData("true == false", false)]
    [InlineData("null == null", true)]
    [InlineData("[1, 2] == [1, 2]", true)]
    [InlineData("[1, 2] == [2, 1]", false)]
    [InlineData("{\"a\": 1} == {\"a\": 1}", true)]
    public void Eval_EqualityOnSameKind_ComparesValues(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("1 == \"1\"", false)]
    [InlineData("1 != \"1\"", true)]
    [InlineData("true == 1", false)]
    [InlineData("null == 0", false)]
    [InlineData("[1] == 1", false)]
    [InlineData("null != false", true)]
    public void Eval_EqualityAcrossKinds_IsFalseNotError(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("\"a\" < \"b\"", "type mismatch: String < String")]
    [InlineData("true < false", "type mismatch: Bool < Bool")]
    [InlineData("1 < \"a\"", "type mismatch: Int < String")]
    [InlineData("[1] >= [1]", "type mismatch: Array >= Array")]
    public void Eval_OrderingOnNonNumbers_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_StringConcatenation_Works()
    {
        EvalTestHelper.AssertString("hello world", "\"hello\" + \" \" + \"world\"");
    }

    [Theory]
    [InlineData("\"a\" + 1", "type mismatch: String + Int")]
    [InlineData("1 + \"a\"", "type mismatch: Int + String")]
    [InlineData("\"a\" * 2", "type mismatch: String * Int")]
    [InlineData("\"a\" + null", "type mismatch: String + Null")]
    public void Eval_StringWithNonString_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("true && true", true)]
    [InlineData("true && false", false)]
    [InlineData("false || true", true)]
    [InlineData("false || false", false)]
    [InlineData("true && false || true", true)]
    [InlineData("!true || true", true)]
    public void Eval_LogicalOperators_ProduceBool(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("false && boom()", false)]
    [InlineData("true || boom()", true)]
    [InlineData("false && (1 / 0 == 1)", false)]
    public void Eval_LogicalShortCircuit_DoesNotEvaluateRightSide(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("1 && true", "operator && requires Bool, got Int")]
    [InlineData("true && 1", "operator && requires Bool, got Int")]
    [InlineData("null || true", "operator || requires Bool, got Null")]
    [InlineData("false || \"x\"", "operator || requires Bool, got String")]
    public void Eval_LogicalOnNonBool_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_LogicalRightSideError_Propagates()
    {
        EvalTestHelper.AssertError("undefined variable: boom", "true && boom()");
    }
}
