using Microsoft.Z3;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Reasoning
{
    internal static class TypeHelper
    {
        public static T CreateSymbol<T>(Context context, string name)
        {
            var classtype = typeof(T);
            var classConstructor = classtype.GetConstructor(new Type[] { typeof(Context), typeof(string) });
            var instance = (T)classConstructor.Invoke(new object[] { context, name });
            return instance;
        }
    }
    public interface ISortVisitor
    {
        void Visit<T>(Symbol<T> symbol) where T : ISort;
        void Visit<T>(Where<T> where);
        void Visit<TSource, TResult>(Select<TSource, TResult> select);
        void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany);

        void Visit<T>(Sequence<T> ite);
    }

    public class LinqVisitor : ISortVisitor
    {
        private readonly Context context;
        internal readonly IList<Bool> constraints;
        internal readonly IList<Tuple<string, Expr>> parameters;
        internal object program;

        public LinqVisitor(Context context)
        {
            this.context = context;
            this.constraints = new List<Bool>();
            this.parameters = new List<Tuple<string, Expr>>();
        }

        public void Visit<T>(Where<T> where)
        {
            where.Source.Accept(this);
            var constraint = where.Predicate((T)this.program);
            this.constraints.Add(constraint);
        }

        public void Visit<T>(Symbol<T> symbol) where T : ISort
        {
            var tmp = symbol.Get(context);
            this.parameters.Add(Tuple.Create(symbol.Name, tmp.Expr));
            this.program = tmp;
        }

        public void Visit<TSource, TResult>(Select<TSource, TResult> select)
        {
            select.Source.Accept(this);
            var a = (TSource)this.program;
            var b = select.Projection(a);
            this.program = b;
        }

        public void Visit<TSource, TCollection, TResult>(SelectMany<TSource, TCollection, TResult> selectmany)
        {
            selectmany.Source.Accept(this);
            var a = (TSource)this.program;

            var b = selectmany.CollectionSelector(a);
            b.Accept(this);
            var c = (TCollection)this.program;

            var result = selectmany.ResultSelector(a, c);
            this.program = result;
        }

        public void Visit<T>(Sequence<T> ite)
        {
            var asolver = new LinqVisitor(context);
            ite.lhs.Accept(asolver);

            var bsolver = new LinqVisitor(context);
            ite.rhs.Accept(bsolver);

            var aconstraints = asolver.constraints.Aggregate((x, y) => x & y);
            var bconstraints = bsolver.constraints.Aggregate((x, y) => x & y);

            var ainterpreted = (T)asolver.program;
            var binterpreted = (T)bsolver.program;

            Contract.Assert(asolver.parameters.SequenceEqual(bsolver.parameters));
            foreach (var p in asolver.parameters)
            {
                this.parameters.Add(p);
            }

            this.constraints.Add(aconstraints | bconstraints);

            Func<Context, Model, T> resolver = (context, model) =>
            {
                var bconstraintsEvaluated = model.Eval(bconstraints.Expr);
                Contract.Assert(bconstraintsEvaluated.IsBool);

                if (bconstraintsEvaluated.IsTrue)
                {
                    return binterpreted;
                }

                var aconstraintsEvaluated = model.Eval(aconstraints.Expr);
                Contract.Assert(aconstraintsEvaluated.IsBool);
                if (aconstraintsEvaluated.IsTrue)
                {
                    return ainterpreted;
                }

                // should not be here if model is generated
                Contract.Assert(false);
                return default(T);
            };

            this.program = resolver;

            int fd = 10;
        }

        private T Resolve<T>(Context context, Model model, T interpreted)
        {
            Type t = typeof(T);

            // compiler generated anonymous type?
            if (t.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
            {
                Contract.Assert(t.GetConstructors().Count() == 1);
                // single constructor
                var ctr = t.GetConstructors().First();
                var args = ctr.GetParameters().Select<ParameterInfo, object>(p =>
                {
                    var z3Exprs = (from i in this.parameters
                                  where p.Name == i.Item1
                                  select i.Item2);
                    if (z3Exprs.Count() == 0)
                    {
                        throw new Exception(string.Format("Could not find formal parameter for anonymous type {0}. Make sure your Symbol<T> variable has the same name as the linq iteration variable!", p.Name));
                    }

                    var z3Expr = z3Exprs.First();
                    var assigned = model.Eval(z3Expr);
                    if (assigned.IsInt)
                    {
                        return new Integer(this.context, ((IntNum)assigned));
                    }
                    else if (assigned.IsBool)
                    {
                        return new Bool(this.context, this.context.MkBool(assigned.IsTrue));
                    }

                    throw new NotImplementedException("Unknown type:" + p.Name);
                });

                return (T)ctr.Invoke(args.ToArray());
            }

            // just a single ISort
            if (interpreted is ISort)
            {
                var tmp = interpreted as ISort;
                var assigned = model.Eval(tmp.Expr);
                tmp.Expr = assigned;
                return (T)tmp;
            }

            // A more complicated array of ISorts
            if (interpreted is ISort[])
            {
                var tmp = interpreted as ISort[];
                var result = (ISort[])Activator.CreateInstance(t, tmp.Length);
                for (int i = 0; i < tmp.Length; i++)
                {
                    var assigned = model.Eval(tmp[i].Expr);
                    var tmp1 = Activator.CreateInstance(t.GetElementType(), context, assigned);
                    result[i] = (ISort)tmp1;
                }

                return (T)(object)result;
            }

            throw new NotImplementedException("I don't know how to convert a solution to your T" + t);
        }

        private T Resolve<T>(Context context, Model model, Func<Context, Model, T> interpreted)
        {
            return this.Resolve<T>(context, model, interpreted(context, model));
        }

        public T Solve<T>(Sort<T> linqtree)
        {
            this.constraints.Clear();
            this.parameters.Clear();

            linqtree.Accept(this);
            var interpreted = this.program;

            var solver = this.context.MkSimpleSolver();

            foreach (var constraint in this.constraints)
            {
                solver.Assert(constraint.Expr as BoolExpr);
            }

            Console.WriteLine(solver);

            Status status = solver.Check();
            if (status != Status.SATISFIABLE)
            {
                throw new Exception("No solution");
            }

            if (interpreted is Func<Context, Model, T>)
            {
                return this.Resolve<T>(this.context, solver.Model, (Func<Context, Model, T>)interpreted);
            }
            else {
                return this.Resolve<T>(this.context, solver.Model, (T) interpreted);
            }
        }
    }

    public class Where<T> : Sort<T>
    {
        internal Sort<T> Source;
        internal Func<T, Bool> Predicate;

        internal override void Accept(ISortVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
    public class Select<TSource, TResult> : Sort<TResult>
    {
        internal Sort<TSource> Source { get; set; }
        internal Func<TSource, TResult> Projection { get; set; }
        internal override void Accept(ISortVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
    public class SelectMany<TSource, TCollection, TResult> : Sort<TResult>
    {
        internal Sort<TSource> Source { get; set; }
        internal Func<TSource, Sort<TCollection>> CollectionSelector { get; set; }
        internal Func<TSource, TCollection, TResult> ResultSelector { get; set; }
        internal override void Accept(ISortVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    public static class Extensions
    {
        public static Sort<TResult> Select<TSource, TResult>(
            this Sort<TSource> first,
            Func<TSource, TResult> projection)
        {
            return new Select<TSource, TResult> { Source = first, Projection = projection };
        }

        public static Sort<TResult> SelectMany<TSource, TCollection, TResult>(
            this Sort<TSource> first,
            Func<TSource, Sort<TCollection>> collectionSelector,
            Func<TSource, TCollection, TResult> resultSelector)
        {
            return new SelectMany<TSource, TCollection, TResult>
            {
                Source = first,
                CollectionSelector = collectionSelector,
                ResultSelector = resultSelector
            };
        }

        public static Sort<T> Where<T>(
            this Sort<T> source,
            Func<T, Bool> predicate)
        {
            return new Where<T> { Source = source, Predicate = predicate };
        }


        public static Sort<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this Sort<TOuter> outer, Sort<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, Sort<TInner>, TResult> resultSelector)
        {
            return null;
        }
        public static Sort<TResult> Join<TOuter, TInner, TKey, TResult>(this Sort<TOuter> outer, Sort<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
        {
            return null;
        }

        public class FunctionalList<T> : IEnumerable<T>
        {
            // Creates a new list that is empty
            public FunctionalList()
            {
                IsEmpty = true;
            }
            // Creates a new list containe value and a reference to tail
            public FunctionalList(T head, FunctionalList<T> tail)
            {
                IsEmpty = false;
                Head = head;
                Tail = tail;
            }
            // Is the list empty?
            public bool IsEmpty { get; private set; }
            // Properties valid for a non-empty list
            public T Head { get; private set; }
            public FunctionalList<T> Tail { get; private set; }

            public IEnumerator<T> GetEnumerator()
            {
                return FunctionalList.Helper<T>(this).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return FunctionalList.Helper<T>(this).GetEnumerator();
            }
        }

        // Static class that provides nicer syntax for creating lists
        public static class FunctionalList
        {
            public static FunctionalList<T> Empty<T>()
            {
                return new FunctionalList<T>();
            }
            public static FunctionalList<T> Cons<T>
                    (T head, FunctionalList<T> tail)
            {
                return new FunctionalList<T>(head, tail);
            }

            internal static IEnumerable<T> Helper<T>(FunctionalList<T> lst)
            {
                if (lst.IsEmpty) yield break;
                yield return lst.Head;
                foreach (var item in Helper(lst.Tail))
                    yield return item;
            }

            public static T[] ToArray<T>(FunctionalList<T> lst)
            {
                var array = Helper<T>(lst).ToArray();
                return array;
            }
        }

        public static Sort<T[]> FromSequence<T>(this IEnumerable<Sort<T>> source)
        {
            Sort<T[]> output = source.Aggregate<Sort<T>, Sort<FunctionalList<T>>, Sort<T[]>>(
                null,
                (i, j) =>
                {
                    if (i == null)
                        return from sample in j
                               select FunctionalList.Cons(sample, FunctionalList.Empty<T>());
                    return from lst in i
                           from sample in j
                           select FunctionalList.Cons(sample, lst);
                },
                uncertainlst =>
                {
                    return from sample in uncertainlst
                           select sample.Reverse().ToArray();
                });
            return output;
        }

        private class Comparer<T> : IEqualityComparer<T>
        {
            private readonly Func<T, T, bool> equals;
            private readonly Func<T, int> hash;

            public Comparer(Func<T, int> hash, Func<T, T, bool> equals)
            {
                this.hash = hash;
                this.equals = equals;
            }
            public bool Equals(T x, T y)
            {
                return this.equals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return this.hash(obj);
            }
        }
        public static IEnumerable<T> Distinct<T>(this IEnumerable<T> source, Func<T, int> hash, Func<T, T, bool> equals)
        {
            return source.Distinct(new Comparer<T>(hash, equals));
        }

        public static Sort<T> Sequence<T>(this Sort<T> a, Sort<T> b)
        {
            return new Sequence<T>(a, b);
        }
        public static Bool Distinct<T>(this T[] symbols) where T : ISort
        {
            var context = symbols.First().Context;
            return new Bool(context, context.MkDistinct(symbols.Select(i => i.Expr).ToArray()));
        }
        public static Bool Distinct<T>(this IEnumerable<T> symbols) where T : ISort
        {
            var context = symbols.First().Context;
            return new Bool(context, context.MkDistinct(symbols.Select(i => i.Expr).ToArray()));
        }
    }


    //public static class ISortStaticMethods
    //{
    //    public static Sort UnderlyingType(this ISort t, Context context)
    //    {
    //        if (t is Integer)
    //            return context.IntSort;
    //        if (t is Bool)
    //            return context.BoolSort;
    //    }
    //}

    public interface ISort
    {
        Expr Expr { get; set; }
        Context Context { get; }
    }

    public abstract class Sort<T>
    {
        internal abstract void Accept(ISortVisitor visitor);
    }

    public class Sequence<T> : Sort<T>
    {
        internal Sort<T> lhs, rhs;
        public Sequence(Sort<T> a, Sort<T> b)
        {
            this.lhs = a;
            this.rhs = b;
        }

        internal override void Accept(ISortVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    public class Symbol<T> : Sort<T> where T : ISort
    {
        internal string Name { get; private set; }
        public Symbol(string name)
        {
            this.Name = name;
        }

        public T Get(Context context)
        {
            return TypeHelper.CreateSymbol<T>(context, this.Name);
        }

        internal override void Accept(ISortVisitor visitor)
        {
            visitor.Visit(this);
        }

        public static Sort<T[]> Variables(int count, string prefix = "x")
        {
            return Enumerable.Range(0, count).Select(i => new Symbol<T>(string.Format("{0}_{1}", prefix, i))).FromSequence();
        }

        public static Sort<T[]> Variables(int count, Func<int, string> f)
        {
            return Enumerable.Range(0, count).Select(i => new Symbol<T>(f(i))).FromSequence();
        }
    }

    public class Bool : ISort
    {
        private BoolExpr expr;
        private readonly Context context;

        public Expr Expr { get { return this.expr; } set { this.expr = value as BoolExpr; } }

        public Sort UnderlyingType { get { return context.BoolSort; } }

        public Context Context
        {
            get
            {
                return this.context;
            }
        }

        public Bool(Context context, string name)
        {
            this.expr = context.MkConst(name, context.BoolSort) as BoolExpr;
            this.context = context;
        }

        public Bool(Context context, Expr e)
        {
            this.expr = (BoolExpr)e;
            this.context = context;
        }

        public static Bool operator &(Bool lhs, Bool rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkAnd(lhs.expr, rhs.expr));
        }

        public static Bool operator |(Bool lhs, Bool rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkOr(lhs.expr, rhs.expr));
        }

        public static Bool operator !(Bool b)
        {
            return new Bool(b.context, b.context.MkNot(b.expr));
        }

        public override string ToString()
        {
            return this.expr.ToString();
        }
    }

    public class Integer : ISort
    {
        private IntExpr expr;
        private readonly Context context;

        public Expr Expr { get { return this.expr; } set { this.expr = value as IntExpr; } }
        public Sort UnderlyingType { get { return context.IntSort; } }
        public Context Context
        {
            get
            {
                return this.context;
            }
        }
        public Integer(Context context, string name)
        {
            this.expr = context.MkConst(name, context.IntSort) as IntExpr;
            this.context = context;
        }

        public Integer(Context context, Expr e)
        {
            this.expr = (IntExpr)e;
            this.context = context;
        }

        #region addition
        public static Integer operator +(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Integer(lhs.context, lhs.context.MkAdd(lhs.expr, rhs.expr) as IntExpr);
        }

        public static Integer operator +(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs + rhsAsExpr;
        }
        public static Integer operator +(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr + rhs;
        }
        #endregion

        #region subtraction
        public static Integer operator -(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Integer(lhs.context, lhs.context.MkSub(lhs.expr, rhs.expr) as IntExpr);
        }

        public static Integer operator -(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs - rhsAsExpr;
        }
        public static Integer operator -(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr - rhs;
        }
        #endregion

        #region multiplication
        public static Integer operator *(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Integer(lhs.context, lhs.context.MkMul(lhs.expr, rhs.expr) as IntExpr);
        }

        public static Integer operator *(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs * rhsAsExpr;
        }
        public static Integer operator *(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr * rhs;
        }
        #endregion

        #region modulus
        public static Integer operator %(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Integer(lhs.context, lhs.context.MkMod(lhs.expr, rhs.expr));
        }

        public static Integer operator %(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs % rhsAsExpr;
        }
        public static Integer operator %(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr % rhs;
        }
        #endregion

        #region comparision
        public static Bool operator <(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkLt(lhs.expr, rhs.expr));
        }
        public static Bool operator >(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkGt(lhs.expr, rhs.expr));
        }

        public static Bool operator <=(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkLe(lhs.expr, rhs.expr));
        }
        public static Bool operator >=(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkGe(lhs.expr, rhs.expr));
        }

        public static Bool operator >(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs > rhsAsExpr;
        }
        public static Bool operator <(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs < rhsAsExpr;
        }
        public static Bool operator >(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr > rhs;
        }
        public static Bool operator <(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr < rhs;
        }

        public static Bool operator >=(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs >= rhsAsExpr;
        }
        public static Bool operator <=(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs <= rhsAsExpr;
        }
        public static Bool operator >=(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr >= rhs;
        }
        public static Bool operator <=(int lhs, Integer rhs)
        {
            var lhsAsExpr = new Integer(rhs.context, rhs.context.MkInt(lhs));
            return lhsAsExpr <= rhs;
        }

        #endregion

        #region equals
        public static Bool operator ==(Integer lhs, Integer rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkEq(lhs.expr, rhs.expr));
        }

        public static Bool operator !=(Integer lhs, Integer rhs)
        {
            return !(lhs == rhs);
        }

        public static Bool operator ==(Integer lhs, int rhs)
        {
            var rhsAsExpr = new Integer(lhs.context, lhs.context.MkInt(rhs));
            return lhs == rhsAsExpr;
        }

        public static Bool operator !=(Integer lhs, int rhs)
        {
            return !(lhs == rhs);
        }

        public static Bool operator ==(int lhs, Integer rhs)
        {
            return rhs == lhs;
        }

        public static Bool operator !=(int lhs, Integer rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

        public override string ToString()
        {
            return this.expr.ToString();
        }
    }

    public class Real : ISort
    {
        private RealExpr expr;
        private readonly Context context;

        public Expr Expr { get { return this.expr; } set { this.expr = value as RealExpr; } }

        public Context Context
        {
            get
            {
                return this.context;
            }
        }

        public Real(Context context, string name)
        {
            this.expr = context.MkConst(name, context.RealSort) as RealExpr;
            this.context = context;
        }

        internal Real(Context context, RealExpr e)
        {
            this.expr = e;
            this.context = context;
        }

        #region addition
        public static Real operator +(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Real(lhs.context, lhs.context.MkAdd(lhs.expr, rhs.expr) as RealExpr);
        }

        public static Real operator +(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs + rhsAsExpr;
        }
        public static Real operator +(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr + rhs;
        }
        #endregion

        #region subtraction
        public static Real operator -(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Real(lhs.context, lhs.context.MkSub(lhs.expr, rhs.expr) as RealExpr);
        }

        public static Real operator -(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs - rhsAsExpr;
        }
        public static Real operator -(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr - rhs;
        }
        #endregion

        #region multiplication
        public static Real operator *(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Real(lhs.context, lhs.context.MkMul(lhs.expr, rhs.expr) as RealExpr);
        }

        public static Real operator *(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs * rhsAsExpr;
        }
        public static Real operator *(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr * rhs;
        }
        #endregion

        #region division
        public static Real operator /(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Real(lhs.context, lhs.context.MkDiv(lhs.expr, rhs.expr) as RealExpr);
        }

        public static Real operator /(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs / rhsAsExpr;
        }
        public static Real operator /(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr / rhs;
        }
        #endregion

        #region comparision
        public static Bool operator <(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkLt(lhs.expr, rhs.expr));
        }
        public static Bool operator >(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkGt(lhs.expr, rhs.expr));
        }

        public static Bool operator <=(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkLe(lhs.expr, rhs.expr));
        }
        public static Bool operator >=(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkGe(lhs.expr, rhs.expr));
        }

        public static Bool operator >(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs > rhsAsExpr;
        }
        public static Bool operator <(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs < rhsAsExpr;
        }
        public static Bool operator >(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr > rhs;
        }
        public static Bool operator <(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr < rhs;
        }

        public static Bool operator >=(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs >= rhsAsExpr;
        }
        public static Bool operator <=(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs <= rhsAsExpr;
        }
        public static Bool operator >=(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr >= rhs;
        }
        public static Bool operator <=(int lhs, Real rhs)
        {
            var lhsAsExpr = new Real(rhs.context, rhs.context.MkReal(lhs));
            return lhsAsExpr <= rhs;
        }

        #endregion

        #region equals
        public static Bool operator ==(Real lhs, Real rhs)
        {
            Contract.Requires(object.ReferenceEquals(lhs.context, rhs.context));
            return new Bool(lhs.context, lhs.context.MkEq(lhs.expr, rhs.expr));
        }

        public static Bool operator !=(Real lhs, Real rhs)
        {
            return !(lhs == rhs);
        }

        public static Bool operator ==(Real lhs, int rhs)
        {
            var rhsAsExpr = new Real(lhs.context, lhs.context.MkReal(rhs));
            return lhs == rhsAsExpr;
        }

        public static Bool operator !=(Real lhs, int rhs)
        {
            return !(lhs == rhs);
        }

        public static Bool operator ==(int lhs, Real rhs)
        {
            return rhs == lhs;
        }

        public static Bool operator !=(int lhs, Real rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

        public override string ToString()
        {
            return this.expr.ToString();
        }
    }

    //public class FiniteSet<T> : ISort where T : ISort
    //{
    //    private readonly Context context;
    //    private readonly Sort sort;
    //    public FiniteSet(Context context, string name)
    //    {
    //        this.sort = context.MkSetSort(default(T).UnderlyingType);
    //        this.Expr = context.MkConst(name, sort);
    //    }

    //    public Sort UnderlyingType
    //    {
    //        get { return this.sort; }
    //    }
    //    public Expr Expr
    //    {
    //        get
    //        {
    //            throw new NotImplementedException();
    //        }

    //        set
    //        {
    //            throw new NotImplementedException();
    //        }
    //    }
    //}

    public class Program
    {
        public static IEnumerable<Tuple<int, int>> Parse()
        {
            var str =
@"..2..1.6.
..7..4...
5.....9..
.1.3.....
8...5...4
.....6.2.
..6.....7
...8..3..
.4.9..2..";
            using (StringReader sr = new StringReader(str))
            {
                string line;
                var j = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    Contract.Assert(line.Length == 9);
                    for (int i = 0; i < 9; i++)
                    {
                        if (line[i] != '.')
                            yield return Tuple.Create(j * 9 + i, Int32.Parse(line[i].ToString()));
                    }
                    j++;
                }
            }
        }
        public static void TestSudoku()
        {
            var model = Parse().ToList();

            var p = from i in Enumerable.Range(0, 81)
                    let row = i % 9
                    let col = i / 9
                    let concrete = model.Find(d => d.Item1 == i)
                    let cell = from cell in new Symbol<Integer>(string.Format("cell_{0}:{1}", row, col))
                               where concrete == null ? cell >= 0 & cell <= 9 : cell == concrete.Item2
                               select cell
                    select cell;

            var p2 = from cells in p.FromSequence()

                         // build row level constraints
                     let rows = from rowIndex in Enumerable.Range(0, 9)
                                    // find all rows in rowIndex
                                let row = from i in Enumerable.Range(0, 81)
                                          where i / 9 == rowIndex
                                          select cells[i]
                                select row.Distinct() // assert distinct
                                                      // assert all rows must be distinct
                     where rows.Aggregate((a, b) => a & b)

                     // ditto but for cols
                     let cols = from colIndex in Enumerable.Range(0, 9)
                                let col = from i in Enumerable.Range(0, 81)
                                          where i % 9 == colIndex
                                          select cells[i]
                                select col.Distinct()
                     where cols.Aggregate((a, b) => a & b)

                     select cells;

            using (var context = new Context())
            {
                var visitor = new LinqVisitor(context);
                var solution = visitor.Solve(p2);

                var map = Enumerable.Range(0, 81).Select(i => new { row = i / 9, col = i % 9, cell = solution[i] }).ToDictionary(k => new { k.row, k.col }, k => k.cell);
                for (int i = 0; i < 9; i++)
                {
                    for (int j = 0; j < 9; j++)
                    {
                        var cell = map[new { row = i, col = j }];
                        Console.Write(cell + " ");
                    }
                    Console.WriteLine();
                }

                int x = 10;
            }
        }

        public static void Testite()
        {
            var a = from i in new Symbol<Integer>("i")
                    where i == 0
                    let n = i + 1
                    select new { i = n };

            var b = from i in new Symbol<Integer>("i")
                    where i == 0
                    select new { i = i + 2 };

            var c = a.Sequence(b);

            using (var context = new Context())
            {
                var visitor = new LinqVisitor(context);
                var result0 = visitor.Solve(a);
                var result1 = visitor.Solve(b);
                var result2 = visitor.Solve(c);

                int x = 10;
            }
        }

        public static void Main()
        {
            Testite();
            //Test();
            TestSudoku();
            // A simple LINQ program that sets up a handful of constraints
            var program0 = from x in new Symbol<Integer>("x")
                           from y in new Symbol<Integer>("y")
                           from a in new Symbol<Bool>("a")
                           where x > 0 & x < 10
                           where y > 0 & y < 10
                           where x < y
                           where x % 3 == 0
                           where !a
                           where x + y + 1 < 13
                           select new { x, y, a };

            // A more complicated LINQ program that:
            //  i)   builds 10 integers using the FromSequence call
            //  ii)  requires the sum of those 10 integers be less than 10
            //  iii) requires that the first variable be greater than the second
            var program2 = from seq in Symbol<Integer>.Variables(10, "x")
                           let sum = seq.Aggregate((a, b) => a + b)
                           where sum < 10
                           where sum > 0
                           where seq[0] > seq[1]
                           select seq;

            //var program3 = from set in new FiniteSet<Integer>

            using (var context = new Context())
            {
                var visitor = new LinqVisitor(context);
                var result0 = visitor.Solve(program0);
                var result2 = visitor.Solve(program2);
                int xx = 10;
            }
        }
    }
}
