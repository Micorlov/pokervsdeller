#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Showdown.Game;

namespace Showdown.Tests.Game
{
    [TestFixture]
    public class MiniJsonTests
    {
        private static object? Parse(string text)
        {
            Assert.IsTrue(MiniJson.TryParse(text, out var value), "should parse: " + text);
            return value;
        }

        [Test]
        public void Parses_objects_arrays_and_scalars_into_the_documented_tree()
        {
            var tree = (Dictionary<string, object?>)Parse("{\"a\":1,\"b\":[true,false,null,\"x\"],\"c\":{\"d\":-2.5}}")!;

            Assert.AreEqual(1.0, tree["a"]);
            var list = (List<object?>)tree["b"]!;
            CollectionAssert.AreEqual(new object?[] { true, false, null, "x" }, list);
            Assert.AreEqual(-2.5, ((Dictionary<string, object?>)tree["c"]!)["d"]);
        }

        [Test]
        public void Tolerates_whitespace_between_tokens()
        {
            var tree = (Dictionary<string, object?>)Parse(" \n\t{ \"a\" : [ 1 , 2 ] }\r\n")!;
            Assert.AreEqual(2, ((List<object?>)tree["a"]!).Count);
        }

        [Test]
        public void The_literal_null_parses_successfully_to_null()
        {
            Assert.IsTrue(MiniJson.TryParse("null", out var value));
            Assert.IsNull(value);
        }

        [TestCase("0", 0.0)]
        [TestCase("-0", 0.0)]
        [TestCase("12", 12.0)]
        [TestCase("1.5", 1.5)]
        [TestCase("1e3", 1000.0)]
        [TestCase("1.25E-2", 0.0125)]
        [TestCase("1700000000000", 1700000000000.0)]
        public void Parses_numbers(string text, double expected)
        {
            Assert.AreEqual(expected, (double)Parse(text)!, 1e-12);
        }

        [Test]
        public void Decodes_string_escapes_and_unicode()
        {
            Assert.AreEqual("a\"b\\c/d\b\f\n\r\tA\u00e9", Parse("\"a\\\"b\\\\c\\/d\\b\\f\\n\\r\\tA\\u00e9\""));
        }

        [Test]
        public void Last_duplicate_key_wins()
        {
            var tree = (Dictionary<string, object?>)Parse("{\"a\":1,\"a\":2}")!;
            Assert.AreEqual(2.0, tree["a"]);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("{\"a\" 1}")]
        [TestCase("{a:1}")]
        [TestCase("[1,]")]
        [TestCase("[1 2]")]
        [TestCase("\"unterminated")]
        [TestCase("\"bad\\q\"")]
        [TestCase("\"bad\\u12\"")]
        [TestCase("\"bad\\uZZZZ\"")]
        [TestCase("tru")]
        [TestCase("nul")]
        [TestCase("01")]
        [TestCase("1.")]
        [TestCase("-")]
        [TestCase("1e")]
        [TestCase("+1")]
        [TestCase("1e999")]
        [TestCase("{} extra")]
        [TestCase("[] []")]
        [TestCase("\"tab\tinside\"")]
        public void Rejects_malformed_input_without_throwing(string text)
        {
            Assert.IsFalse(MiniJson.TryParse(text, out var value));
            Assert.IsNull(value);
        }

        [Test]
        public void Rejects_null_text()
        {
            Assert.IsFalse(MiniJson.TryParse(null, out _));
        }

        [Test]
        public void Rejects_nesting_deeper_than_the_limit_instead_of_overflowing_the_stack()
        {
            var deep = new string('[', MiniJson.MaxDepth + 10) + new string(']', MiniJson.MaxDepth + 10);
            Assert.IsFalse(MiniJson.TryParse(deep, out _));

            var shallow = new string('[', MiniJson.MaxDepth) + new string(']', MiniJson.MaxDepth);
            Assert.IsTrue(MiniJson.TryParse(shallow, out _));
        }

        [Test]
        public void Writes_compact_json_in_insertion_order()
        {
            var tree = new Dictionary<string, object?>
            {
                { "z", 1 },
                { "a", new List<object?> { true, null, "s" } },
                { "n", new Dictionary<string, object?>() },
            };
            Assert.AreEqual("{\"z\":1,\"a\":[true,null,\"s\"],\"n\":{}}", MiniJson.Serialize(tree));
        }

        [Test]
        public void Writes_whole_doubles_without_exponent_or_fraction()
        {
            Assert.AreEqual("[500,1700000000000,-3,0.5]", MiniJson.Serialize(new object[] { 500.0, 1700000000000.0, -3.0, 0.5 }));
            Assert.AreEqual("[7,9000000000]", MiniJson.Serialize(new object[] { 7, 9000000000L }));
        }

        [Test]
        public void Writes_non_finite_numbers_as_null_like_JSON_stringify()
        {
            Assert.AreEqual("[null,null]", MiniJson.Serialize(new object[] { double.NaN, double.PositiveInfinity }));
        }

        [Test]
        public void Escapes_quotes_backslashes_and_control_characters()
        {
            Assert.AreEqual("\"a\\\"b\\\\c\\n\\u0001\"", MiniJson.Serialize("a\"b\\c\n\u0001"));
        }

        [Test]
        public void Refuses_to_write_a_type_it_cannot_represent()
        {
            Assert.Throws<System.ArgumentException>(() => MiniJson.Serialize(new object()));
        }

        [Test]
        public void Round_trips_a_nested_tree()
        {
            const string text = "{\"a\":[1,2.5,{\"b\":\"x\\ny\"}],\"c\":null,\"d\":true,\"e\":\"\\u00e9\"}";
            var once = MiniJson.Serialize(Parse(text));
            var twice = MiniJson.Serialize(Parse(once));
            Assert.AreEqual(once, twice);
            Assert.AreEqual("{\"a\":[1,2.5,{\"b\":\"x\\ny\"}],\"c\":null,\"d\":true,\"e\":\"\u00e9\"}", once);
        }
    }
}
