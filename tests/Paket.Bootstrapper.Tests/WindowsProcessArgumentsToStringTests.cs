using NUnit.Framework;

namespace Paket.Bootstrapper.Tests
{
    [TestFixture]
    public class WindowsProcessArgumentsToStringTests
    {
        private void Verify(string expected, params string[] argv)
        {
            var result = WindowsProcessArguments.ToString(argv);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Multiple_parameters_are_separated_by_spaces()
        {
            Verify("Hello World", "Hello", "World");
        }

        [Test]
        public void No_quotes_are_added_when_not_needed()
        {
            Verify("Hello_World", "Hello_World");
        }

        [Test]
        public void Quotes_are_added_when_arg_contains_space()
        {
            Verify(@"""Hello World""", "Hello World");
        }

        [Test]
        public void Quote_is_escaped_inside()
        {
            Verify(@"Hello\""World", @"Hello""World");
        }

        [Test]
        public void Quote_is_escaped_at_start()
        {
            Verify(@"\""HelloWorld", @"""HelloWorld");
        }

        [Test]
        public void Quote_is_escaped_at_end()
        {
            Verify(@"HelloWorld\""", @"HelloWorld""");
        }

        [Test]
        public void Backslash_alone_not_escaped()
        {
            Verify(@"Hello\World", @"Hello\World");
        }

        [Test]
        public void Backslash_escaped_if_at_end_and_need_quote()
        {
            Verify(@"""Hello World\\""", @"Hello World\");
        }

        [Test]
        public void Backslash_not_escaped_if_at_end_and_no_need_to_need_quote()
        {
            Verify(@"Hello_World\", @"Hello_World\");
        }

        [Test]
        public void Backslash_before_quote_escaped()
        {
            Verify(@"Hello\\\""World", @"Hello\""World");
        }

        [Test]
        public void Odd_backslash_escaped()
        {
            Verify(@"""a\\\\b c"" d e", @"a\\b c", "d", "e");
        }

        [Test]
        public void Even_backslash_escaped()
        {
            Verify(@"""a\\\\\b c"" d e", @"a\\\b c", "d", "e");
        }

        [Test]
        public void Pass_empty_arguments()
        {
            Verify(@"a """" b", "a", "", "b");
        }
    }
}
