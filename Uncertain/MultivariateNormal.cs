using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Properties;
using MathNet.Numerics.Random;

namespace Microsoft.Research.Uncertain
{
    public class MultivariateNormal : RandomPrimitive<Matrix<double>>
    {
        System.Random _random;

        /// <summary>
        /// The mean of the matrix normal distribution.
        /// </summary>
        readonly Matrix<double> _m;

        /// <summary>
        /// The covariance matrix for the rows.
        /// </summary>
        readonly Matrix<double> _v;

        /// <summary>
        /// The covariance matrix for the columns.
        /// </summary>
        readonly Matrix<double> _k;

        /// <summary>
        /// Initializes a new instance of the <see cref="MatrixNormal"/> class.
        /// </summary>
        /// <param name="m">The mean of the matrix normal.</param>
        /// <param name="v">The covariance matrix for the rows.</param>
        /// <param name="k">The covariance matrix for the columns.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the dimensions of the mean and two covariance matrices don't match.</exception>
        public MultivariateNormal(Matrix<double> m, Matrix<double> v, Matrix<double> k)
        {
            if (MathNet.Numerics.Control.CheckDistributionParameters && !IsValidParameterSet(m, v, k))
            {
                throw new ArgumentException(Resources.InvalidDistributionParameters);
            }

            _random = SystemRandomSource.Default;
            _m = m;
            _v = v;
            _k = k;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatrixNormal"/> class.
        /// </summary>
        /// <param name="m">The mean of the matrix normal.</param>
        /// <param name="v">The covariance matrix for the rows.</param>
        /// <param name="k">The covariance matrix for the columns.</param>
        /// <param name="randomSource">The random number generator which is used to draw random samples.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the dimensions of the mean and two covariance matrices don't match.</exception>
        public MultivariateNormal(Matrix<double> m, Matrix<double> v, Matrix<double> k, System.Random randomSource)
        {
            if (MathNet.Numerics.Control.CheckDistributionParameters && !IsValidParameterSet(m, v, k))
            {
                throw new ArgumentException(Resources.InvalidDistributionParameters);
            }

            _random = randomSource ?? SystemRandomSource.Default;
            _m = m;
            _v = v;
            _k = k;
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "MatrixNormal(Rows = " + _m.RowCount + ", Columns = " + _m.ColumnCount + ")";
        }

        /// <summary>
        /// Tests whether the provided values are valid parameters for this distribution.
        /// </summary>
        /// <param name="m">The mean of the matrix normal.</param>
        /// <param name="v">The covariance matrix for the rows.</param>
        /// <param name="k">The covariance matrix for the columns.</param>
        public static bool IsValidParameterSet(Matrix<double> m, Matrix<double> v, Matrix<double> k)
        {
            var n = m.RowCount;
            var p = m.ColumnCount;
            if (v.ColumnCount != n || v.RowCount != n)
            {
                return false;
            }

            if (k.ColumnCount != p || k.RowCount != p)
            {
                return false;
            }

            for (var i = 0; i < v.RowCount; i++)
            {
                if (v.At(i, i) <= 0)
                {
                    return false;
                }
            }

            for (var i = 0; i < k.RowCount; i++)
            {
                if (k.At(i, i) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the mean. (M)
        /// </summary>
        /// <value>The mean of the distribution.</value>
        public Matrix<double> Mean
        {
            get { return _m; }
        }

        /// <summary>
        /// Gets the row covariance. (V)
        /// </summary>
        /// <value>The row covariance.</value>
        public Matrix<double> RowCovariance
        {
            get { return _v; }
        }

        /// <summary>
        /// Gets the column covariance. (K)
        /// </summary>
        /// <value>The column covariance.</value>
        public Matrix<double> ColumnCovariance
        {
            get { return _k; }
        }

        /// <summary>
        /// Gets or sets the random number generator which is used to draw random samples.
        /// </summary>
        public System.Random RandomSource
        {
            get { return _random; }
            set { _random = value ?? SystemRandomSource.Default; }
        }

        /// <summary>
        /// Evaluates the probability density function for the matrix normal distribution.
        /// </summary>
        /// <param name="x">The matrix at which to evaluate the density at.</param>
        /// <returns>the density at <paramref name="x"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">If the argument does not have the correct dimensions.</exception>
        public double Density(Matrix<double> x)
        {
            if (x.RowCount != _m.RowCount || x.ColumnCount != _m.ColumnCount)
            {
                throw new Exception("dimension mismatch");
            }

            var a = x - _m;
            var cholV = _v.Cholesky();
            var cholK = _k.Cholesky();

            return Math.Exp(-0.5 * cholK.Solve(a.Transpose() * cholV.Solve(a)).Trace())
                   / Math.Pow(2.0 * MathNet.Numerics.Constants.Pi, x.RowCount * x.ColumnCount / 2.0)
                   / Math.Pow(cholK.Determinant, x.RowCount / 2.0)
                   / Math.Pow(cholV.Determinant, x.ColumnCount / 2.0);
        }

        /// <summary>
        /// Samples a matrix normal distributed random variable.
        /// </summary>
        /// <param name="rnd">The random number generator to use.</param>
        /// <param name="m">The mean of the matrix normal.</param>
        /// <param name="v">The covariance matrix for the rows.</param>
        /// <param name="k">The covariance matrix for the columns.</param>
        /// <exception cref="ArgumentOutOfRangeException">If the dimensions of the mean and two covariance matrices don't match.</exception>
        /// <returns>a sequence of samples from the distribution.</returns>
        public static Matrix<double> Sample(System.Random rnd, Matrix<double> m, Matrix<double> v, Matrix<double> k)
        {
            if (MathNet.Numerics.Control.CheckDistributionParameters && !IsValidParameterSet(m, v, k))
            {
                throw new ArgumentException(Resources.InvalidDistributionParameters);
            }

            var n = m.RowCount;
            var p = m.ColumnCount;

            // Compute the Kronecker product of V and K, this is the covariance matrix for the stacked matrix.
            var vki = v.KroneckerProduct(k.Inverse());

            // Sample a vector valued random variable with VKi as the covariance.
            var vector = SampleVectorNormal(rnd, new DenseVector(n * p), vki);

            // Unstack the vector v and add the mean.
            var r = m.Clone();
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < p; j++)
                {
                    r.At(i, j, r.At(i, j) + vector[(j * n) + i]);
                }
            }

            return r;
        }

        /// <summary>
        /// Samples a vector normal distributed random variable.
        /// </summary>
        /// <param name="rnd">The random number generator to use.</param>
        /// <param name="mean">The mean of the vector normal distribution.</param>
        /// <param name="covariance">The covariance matrix of the vector normal distribution.</param>
        /// <returns>a sequence of samples from defined distribution.</returns>
        static Vector<double> SampleVectorNormal(System.Random rnd, Vector<double> mean, Matrix<double> covariance)
        {
            var chol = covariance.Cholesky();

            // Sample a standard normal variable.
            var v = Vector<double>.Build.Random(mean.Count, new Normal(rnd));

            // Return the transformed variable.
            return mean + (chol.Factor * v);
        }

        public override double Score(Matrix<double> t)
        {
            return Density(t);
        }

        /// <summary>
        /// Samples a matrix normal distributed random variable.
        /// </summary>
        /// <returns>A random number from this distribution.</returns>
        public override Matrix<double> GetSample()
        {
            return Sample(_random, _m, _v, _k);
        }

        public override IEnumerable<Weighted<Matrix<double>>> GetSupport()
        {
            throw new Exception("Infinite Support!");
        }

        public override int GetStructuralHash()
        {
            return _m.GetHashCode() + _k.GetHashCode() + _v.GetHashCode();
        }

        public override bool StructuralEquals(RandomPrimitive other)
        {
            if (other is MultivariateNormal)
            {
                var tmp = other as MultivariateNormal;
                return tmp._m == this._m && tmp._v == this._v && tmp._k == this._k;
            }
            return false;
        }
    }
}