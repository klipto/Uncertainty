/* ------------------------------------------ START OF LICENSE -----------------------------------------
* UncertainT
*
* Copyright(c) Microsoft Corporation
*
* All rights reserved.
*
* MIT License
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the ""Software""), to 
* deal in the Software without restriction, including without limitation the 
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
* sell copies of the Software, and to permit persons to whom the Software is 
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in 
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
* SOFTWARE.
* ----------------------------------------------- END OF LICENSE ------------------------------------------
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.Willis
{
    public class Parser
    {

        public interface RegularExpression { }

        internal class Epsilon : RegularExpression
        {
            public override string ToString()
            {
                return "epsilon";
            }
        }
        internal class Primitive : RegularExpression
        {
            public char Lo { get; set; }
            public char Hi { get; set; }
            public int Id { get; set; }

            public override string ToString()
            {
                if (this.Lo == this.Hi)
                    return this.Lo.ToString();
                else
                    return String.Format("[{0}-{1}]", this.Lo, this.Hi);
            }
        }
        internal class Sequence : RegularExpression
        {
            public RegularExpression Left { get; set; }
            public RegularExpression Right { get; set; }

            public override string ToString()
            {
                return String.Format("{0}{1}", this.Left.ToString(), this.Right.ToString());
            }
        }
        internal class Group : RegularExpression
        {
            public RegularExpression Re { get; set; }

            public override string ToString()
            {
                return this.Re.ToString();
            }
        }
        internal class Or : RegularExpression
        {
            public RegularExpression Left { get; set; }
            public RegularExpression Right { get; set; }
            public override string ToString()
            {
                return String.Format("{0}|{1}", this.Left.ToString(), this.Right.ToString());
            }
        }
        internal class Star : RegularExpression
        {
            public bool Greedy { get; set; }
            public RegularExpression Re { get; set; }
            public override string ToString()
            {
                return String.Format("({0})*{1}", this.Re.ToString(), this.Greedy ? "" : "?");
            }
        }
        internal class Question : RegularExpression
        {
            public bool Greedy { get; set; }
            public RegularExpression Re { get; set; }
            public override string ToString()
            {
                return String.Format("({0})?{1}", this.Re.ToString(), this.Greedy ? "" : "?");
            }
        }
        internal class Plus : RegularExpression
        {
            public bool Greedy { get; set; }
            public RegularExpression Re { get; set; }
            public override string ToString()
            {
                return String.Format("({0})+{1}", this.Re.ToString(), this.Greedy ? "" : "?");
            }
        }

        private string input;
        private int counter;
        private int pos;

        public Parser(string input)
        {
            this.pos = 0;
            this.input = input;
            this.counter = 0;
        }

        private char Peek()
        {
            return this.input[this.pos];
        }

        private void Eat(char item)
        {
            if (this.Peek() == item)
                this.pos++;
            else
                throw new Exception(String.Format("Expected {0}; got {1}", item, this.Peek()));
        }

        private char Next()
        {
            var tmp = this.Peek();
            this.Eat(tmp);
            return tmp;
        }

        private bool More()
        {
            return this.pos < this.input.Length;
        }

        private RegularExpression Atom()
        {
            switch (this.Peek())
            {
                case '(':
                    {
                        this.Eat('(');
                        var tmp = this.Regexp();
                        this.Eat(')');
                        return new Group { Re = tmp };
                    }
                case '[':
                    {
                        this.Eat('[');
                        var lo = this.Next();
                        this.Eat('-');
                        var hi = this.Next();
                        this.Eat(']');
                        return new Primitive { Lo = lo, Hi = hi, Id = this.counter++ };
                    }
                case '\\':
                    {
                        this.Eat('\\');
                        var escaped = this.Next();
                        return new Primitive { Lo = escaped, Hi = escaped, Id = this.counter++ };
                    }
                case '.':
                    {
                        this.Eat('.');
                        return new Primitive { Lo = Char.MinValue, Hi = Char.MaxValue, Id = this.counter++ };
                    }
                default:
                    {
                        var tmp = this.Next();
                        return new Primitive { Lo = tmp, Hi = tmp, Id = this.counter++ };
                    }
            }
        }
        private RegularExpression Factor()
        {
            var a = this.Atom();
            while (this.More() && (this.Peek() == '*' || this.Peek() == '?' || this.Peek() == '+'))
            {
                switch (this.Peek())
                {
                    case '*':
                        {
                            this.Eat('*');
                            var greedy = true;
                            if (this.More() && this.Peek() == '?')
                            {
                                greedy = false;
                                this.Eat('?');
                            }
                            a = new Star { Greedy = greedy, Re = a };
                            break;
                        }
                    case '?':
                        {
                            this.Eat('?');
                            var greedy = true;
                            if (this.More() && this.Peek() == '?')
                            {
                                greedy = false;
                                this.Eat('?');
                            }
                            a = new Question { Greedy = greedy, Re = a };
                            break;
                        }
                    case '+':
                        {
                            this.Eat('+');
                            var greedy = true;
                            if (this.More() && this.Peek() == '?')
                            {
                                greedy = false;
                                this.Eat('?');
                            }
                            a = new Plus { Greedy = greedy, Re = a };
                            break;
                        }
                }
            }

            return a;
        }
        private RegularExpression Term()
        {
            RegularExpression f = null;
            while (this.More() && this.Peek() != ')' && this.Peek() != '|')
            {
                var nextf = this.Factor();
                if (f == null)
                    f = nextf;
                else
                    f = new Sequence { Left = f, Right = nextf };
            }
            if (f == null)
                throw new Exception("Expected a term");
            return f;
        }
        private RegularExpression Regexp()
        {
            var t = this.Term();
            if (this.More() && this.Peek() == '|')
            {
                this.Eat('|');
                var r = this.Regexp();
                return new Or { Left = t, Right = r };
            }
            return t;
        }

        public RegularExpression Parse()
        {
            return this.Regexp();
        }

        static Dictionary<string, bool> cache;
        static Parser()
        {
            cache = new Dictionary<string, bool>();
        }
        public static bool IsExpression(string input)
        {

            if (cache.ContainsKey(input))
                return cache[input];
            try
            {
                var ignored2 = new Parser(input).Parse();
                cache[input] = true;
                return true;
            }
            catch (Exception _)
            {
                cache[input] = false;
                return false;
            }
        }
    }
}
