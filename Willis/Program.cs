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
using Microsoft.Research.Uncertain;
using Microsoft.Research.Uncertain.Inference;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Research.Willis
{

    public static class Extensions
    {
        public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(this IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
                emptyProduct,
                (accumulator, sequence) =>
                    from accseq in accumulator
                    from item in sequence
                    select accseq.Concat(new[] { item })
                );
        }
    }

    class Program
    {
        static Uncertain<bool> Expertprior()
        {
            return new Flip(0.2);
        }
        // likelihood of utterance given whether speaker is an expert
        static double RegexpMeaning(string utterance, string derivation, bool isexpert)
        {

            if (isexpert)
                if (utterance == derivation)
                    return 0.99;
                else
                    return 0.01;

            if (utterance == derivation)
                return 0.8;
            return 0.2;
        }

        static IEnumerable<string> PossibleInterpretationsHelper(string input, int depth)
        {
            yield return input;
            if (depth == 0)
            {
                yield break;
            }

            var chars = Enumerable.Range('a', 'z' - 'a' + 1).Select(k => (char)k).ToList();
            //var chars = new FiniteEnumeration<char>(tmp);

            Func<string, IEnumerable<string>> insert = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));

                var strs = from pos in positions
                           from c in chars
                           from b in new[] { true, false }
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, IEnumerable<string>> delete = str =>
            {
                var strs = from pos in Enumerable.Range(0, str.Length)
                           where Char.IsLetterOrDigit(str[pos])
                           let copy = str.Remove(pos, 1)
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, IEnumerable<string>> substitute = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));
                var strs = from pos in positions
                           from c in chars
                           from b in new[] { true, false }
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Remove(pos, 1).Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, int, bool> IsGreedy = (str, starpos) =>
            {
                if (starpos + 1 == str.Length) return true; // last position is not a ?
                if (str[starpos + 1] == '?')
                    return false; // found a *? so this is a nongreedy match
                return true; // just a star in isolation
            };

            Func<string, IEnumerable<string>> makenongreedy = str =>
            {
                var starpos = str.Select((k, i) => Tuple.Create(k, i)).Where(i => i.Item1 == '*').Select(i => i.Item2).Where(k => IsGreedy(str, k)).ToList();
                var strs = from pos in starpos
                               //where IsGreedy(str, pos) == true
                           let copy = str.Remove(pos, 1).Insert(pos, "*?")
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, IEnumerable<string>> makegreedy = str =>
            {
                var starpos = str.Select((k, i) => Tuple.Create(k, i)).Where(i => i.Item1 == '*').Select(i => i.Item2).Where(k => !IsGreedy(str, k)).ToList();
                var strs = from pos in starpos
                               //where IsGreedy(str, pos) == false
                           let copy = str.Remove(pos, 2).Insert(pos, "*")
                           where copy != input
                           select copy;
                return strs;
            };

            var edits = new[] { insert, delete, substitute, makenongreedy, makegreedy };

            var output = from edit in edits
                         from editedinput in edit(input)
                         where Parser.IsExpression(editedinput)
                         select editedinput;

            var recurse = from edit in output
                          from next in PossibleInterpretationsHelper(edit, depth - 1)
                          select next;

            foreach (var item in recurse)
                yield return item;
        }

        static Uncertain<string> PossibleInterpretations2(string input)
        {
            var tmp = Enumerable.Range('a', 'z' - 'a' + 1).Select(k => (char)k).ToList();
            var chars = new Multinomial<char>(tmp);

            Func<string, Uncertain<string>> insert = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));
                if (positions.Count() == 0) return String.Empty;
                var strs = from pos in new Multinomial<int>(positions.ToList())
                           from c in chars
                           from b in new Flip(0.5)
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, Uncertain<string>> delete = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));
                if (positions.Count() == 0) return String.Empty;
                var strs = from pos in new Multinomial<int>(positions.ToList())
                           let copy = str.Remove(pos, 1)
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, Uncertain<string>> substitute = str =>
            {
                var positions = Enumerable.Range(0, str.Length).Where(k => Char.IsLetterOrDigit(str[k]));
                if (positions.Count() == 0) return String.Empty;

                var strs = from pos in new Multinomial<int>(positions.ToList())
                           from c in chars
                           from b in new Flip(0.5)
                           let edit = b ? Char.ToUpper(c) : c
                           let copy = str.Remove(pos, 1).Insert(pos, edit.ToString())
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, int, bool> IsGreedy = (str, starpos) =>
            {
                if (starpos + 1 == str.Length) return true; // last position is not a ?
                if (str[starpos + 1] == '?')
                    return false; // found a *? so this is a nongreedy match
                return true; // just a star in isolation
            };

            Func<string, Uncertain<string>> makenongreedy = str =>
            {
                var starpos = str.Select((k, i) => Tuple.Create(k, i)).Where(i => i.Item1 == '*').Select(i => i.Item2).Where(k => IsGreedy(str, k)).ToList();
                if (starpos.Count == 0) return String.Empty;

                var strs = from pos in new Multinomial<int>(starpos)
                           let copy = str.Remove(pos, 1).Insert(pos, "*?")
                           where copy != input
                           select copy;
                return strs;
            };

            Func<string, Uncertain<string>> makegreedy = str =>
            {
                var starpos = str.Select((k, i) => Tuple.Create(k, i)).Where(i => i.Item1 == '*').Select(i => i.Item2).Where(k => !IsGreedy(str, k)).ToList();
                if (starpos.Count == 0) return String.Empty;
                var strs = from pos in new Multinomial<int>(starpos)
                           let copy = str.Remove(pos, 2).Insert(pos, "*")
                           where copy != input
                           select copy;
                return strs;
            };

            var edits = new Multinomial<Func<string, Uncertain<string>>>(new[] { insert, delete, substitute, makenongreedy, makegreedy });

            var output = from edit in edits
                         from editedinput in edit(input)
                         where editedinput != String.Empty && Parser.IsExpression(editedinput)
                         select editedinput;
            
            var recurse = from edit in output
                          from b in new Flip(0.1)
                          let next = b ? PossibleInterpretations2(edit) : edit
                          from recursivelyedited in next
                          select recursivelyedited;
            return recurse;
        }

        static Uncertain<string> PossibleInterpretations(string input, int depth)
        {
            return new Multinomial<string>(PossibleInterpretationsHelper(input, depth).ToList());
        }

        static Uncertain<bool> LiteralCompiler(string utterance, string derivation)
        {
            var program = from isexpert in Expertprior()
                          let prob = RegexpMeaning(utterance, derivation, isexpert)
                          select new Weighted<bool>()
                          {
                              Value = isexpert,
                              Probability = prob
                          };
            return program.Inference();
        }

        static Uncertain<string> RegexpSpeaker(string utterance, bool isexpert)
        {
            var program = from derivation in PossibleInterpretations(utterance, isexpert ? 1 : 2) // all possible interpretations of regular expression
                          from expertise in LiteralCompiler(utterance, derivation) // infer expertise given statement
                          where expertise == isexpert
                          select derivation;
            return program.Inference();
        }

        static Uncertain<string> CompilerListener(string utterance)
        {
            var program = from isexpert in Expertprior() // prior over expertise
                          from stmt in RegexpSpeaker(utterance, isexpert) // how likely is statement (derived from utterance) given expertise
                          select stmt;

            return program.Inference();
        }

        static Uncertain<string> CompilerListenerWithExample(string utterance, IList<Tuple<string, IEnumerable<Tuple<int, int, int>>>> examples)//string input, IEnumerable<Tuple<int,int,int>> correct)
        {
            var tmp = new Multinomial<Tuple<string, IEnumerable<Tuple<int, int, int>>>>(examples);
            var program = from stmt in PossibleInterpretations2(utterance) //PossibleInterpretations(utterance, 2)
                          let re = new Parser(stmt).Parse()
                          let codes = new Compiler().Compile(re)
                          from example in tmp
                          let matches = new Interpreter().Run(codes.ToList(), example.Item1)
                          where Cmp(matches, example.Item2)
                          select stmt;

            return program.SampledInference(10000);
        }

        static Func<IEnumerable<Tuple<int, int, int>>, IEnumerable<Tuple<int, int, int>>, bool> Cmp = (a, b) =>
        {
            if (a.Count() != b.Count()) return false;
            foreach (var pair in a.Zip(b, Tuple.Create))
            {
                if (pair.Item1.Equals(pair.Item2) == false)
                    return false;
            }

            return true;
        };

        static void Main(string[] args)
        {
            var program = "(a.*)(a.*)(c)";
            
            var example = "aaAaac";
            var re = new Parser("(a.*)(A.*)(c)").Parse();
            var codes = new Compiler().Compile(re).ToList();
            var matches = new Interpreter().Run(codes, example);
            var example1 = Tuple.Create(example, matches);
            var examples = new[] { Tuple.Create(example, matches) };

            //foreach (var item in RegexpSpeaker(program, true).Support().OrderByDescending(k => k.Probability).Take(5))
            //    Console.WriteLine(item);

            //Console.WriteLine();

            //foreach (var item in RegexpSpeaker(program, false).Support().OrderByDescending(k => k.Probability).Take(5))
            //    Console.WriteLine(item);

            //Console.WriteLine();

            //foreach (var item in CompilerListener(program).Support().OrderByDescending(k => k.Probability).Take(5))
            //    Console.WriteLine(item);

            //Console.WriteLine();

            //foreach (var item in CompilerListenerWithExample(program, examples).Support().OrderByDescending(k => k.Probability)) //;/.Take(5))
            //    Console.WriteLine(item);

            //var tmp = new Multinomial<Tuple<string, IEnumerable<Tuple<int, int, int>>>>(examples);
            var p = from stmt in PossibleInterpretations2("(.*)(.*)(c)")
                    let re1 = new Parser(stmt).Parse()
                    let codes1 = new Compiler().Compile(re1)
                    let matches1 = new Interpreter().Run(codes1.ToList(), example1.Item1)
                    where Cmp(matches1, example1.Item2)
                    select stmt;

            var sampler = new MarkovChainMonteCarloSampler<string>(p);
            var output = sampler.Take(1).ToList();

            //var output = PossibleInterpretations2(program).SampledInference(100000).Support().OrderByDescending(k => k.Probability).Take(5).ToList();

            //Console.WriteLine();
            //var output = PossibleInterpretations2(program).SampledInference(100000).Support().OrderByDescending(k => k.Probability).Take(5);
            //foreach (var item in output)
            //    Console.WriteLine(item);

            int x = 10;
        }
    }
}
