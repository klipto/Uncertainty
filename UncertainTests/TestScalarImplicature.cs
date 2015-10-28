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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System.Collections.Generic;

namespace UncertainTests
{

    public class Implicature
    {

        static Uncertain<int> RandomInt4()
        {
            Func<int, int, int> pow = (x, y) => (int)Math.Pow(x, y);
            return from x0 in new Flip(0.5)
                   from x1 in new Flip(0.5)
                   let digit =
                    (x0 ? pow(2, 0) : 0) +
                    (x1 ? pow(2, 1) : 0)
                   select digit;
        }

        public enum Utterance { Some, All, None }

        public static Uncertain<int> WorldPrior()
        {
            var program = from a in RandomInt4() select a;
            return program;
        }

        static bool Meaning(Utterance utt, int num_nice_people)
        {
            // note scalar implicature: some _implies_ "not all" even
            // though logically some includes "all"            
            return utt == Utterance.Some ? num_nice_people > 0 :
                utt == Utterance.All ? num_nice_people == 3 :
                utt == Utterance.Some ? num_nice_people == 0 : true;
        }

        public static Uncertain<int> LiteralListener(Utterance u)
        {
            var program = from num_nice_people in WorldPrior()
                          let meaning = Meaning(u, num_nice_people)
                          where meaning
                          select num_nice_people;

            return program.Inference();
        }

        static Uncertain<Utterance> Speaker(int num_nice_people)
        {
            var program = from utt in UtterancePrior()
                          from w in LiteralListener(utt) // note assumes LiteralListener of the world
                          where w == num_nice_people // optimization: implied by factor statement
                          select utt;
            return program.Inference();
        }


        static Uncertain<Utterance> UtterancePrior()
        {
            IList<Utterance> tmp = new[] { Utterance.Some, Utterance.All, Utterance.None };
            return new Multinomial<Utterance>(tmp);
        }

        public static Uncertain<int> Listener(Utterance utt)
        {
            var program = from num_nice_people in WorldPrior()
                          from stmt in Speaker(num_nice_people)
                          where utt == stmt
                          select num_nice_people;

            return program.Inference();
        }
    }
    [TestClass]
    public class TestScalarImplicature
    {

        private static bool ApproxEqual(double a, double b)
        {
            const double eps = 0.05;
            var diff = a - b;
            return diff > 0.0 - eps && diff < 0.0 + eps;
        }


        [TestMethod]
        public void TestMethod1()
        {
            // Person says 'there are _some_ nice people in the world'
            // Given the world has 4 people (very simple world)
            // _some_ implies not all -- but a literal listener 
            // does not infer this implication.
            Uncertain<int> literaloutput = Implicature.LiteralListener(Implicature.Utterance.Some);
            var literalresult = new[] { 0.33, 0.33, 0.33 };

            foreach (var item in literaloutput.Support().Zip(literalresult, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2));

            // Here we frame the listener as someone that reasons
            // about a speaker's implication. In other words, 
            // we infer that the person is implying that not all 
            // people are nice
            Uncertain<int> inferredoutput = Implicature.Listener(Implicature.Utterance.Some);
            var inferredresult = new[] { 42, 0.42, 0.15 };

            foreach (var item in literaloutput.Support().OrderBy(k => k.Value).Zip(literalresult, Tuple.Create))
                Assert.IsTrue(ApproxEqual(item.Item1.Probability, item.Item2));
        }
    }
}
