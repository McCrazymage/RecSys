﻿using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using RecSys.Evaluation;
using RecSys.Numerical;
using System;
using System.Diagnostics;

namespace RecSys
{
    /// <summary>
    /// The Non-negative Matrix Factorization
    /// See Lee, D. D., & Seung, H. S. (2001). Algorithms for non-negative matrix factorization. NIPS.
    /// and Koren, Y., Bell, R., & Volinsky, C. (2009). Matrix factorization techniques for recommender systems. Computer.
    /// </summary>
    public class NMF
    {
        public static DataMatrix PredictRatings(DataMatrix R_train, DataMatrix R_unknown,
            int maxEpoch, double learnRate, double regularization, int factorCount)
        {
            int userCount = R_train.UserCount;
            int itemCount = R_train.ItemCount;
            int ratingCount = R_train.NonZerosCount;
            double meanOfGlobal = R_train.GetGlobalMean();
            DataMatrix R_train_unknown = R_train.IndexesOfNonZeroElements();  // For testing convergence

            // User latent vectors with default seed
            Matrix<double> P = Utils.CreateRandomMatrixFromNormal(userCount, factorCount, 0, 0.1, Config.Seed);
           // Matrix<double> P = Utils.CreateRandomMatrixFromUniform(userCount, factorCount, 0, 0.1, Config.Seed);
            // Item latent vectors with a different seed
            Matrix<double> Q = Utils.CreateRandomMatrixFromNormal(factorCount, itemCount, 0, 0.1, Config.Seed + 1);
            //Matrix<double> Q = Utils.CreateRandomMatrixFromUniform(factorCount, itemCount, 0, 0.1, Config.Seed + 1);

            // SGD
            double e_prev = double.MaxValue;
            for (int epoch = 0; epoch < maxEpoch; ++epoch)
            {
                foreach (Tuple<int, int, double> element in R_train.Ratings)
                {
                    int indexOfUser = element.Item1;
                    int indexOfItem = element.Item2;
                    double rating = element.Item3;

                    double e_ij = rating - (meanOfGlobal + P.Row(indexOfUser).DotProduct(Q.Column(indexOfItem)));
                    
                    // Update feature vectors
                    Vector<double> P_u = P.Row(indexOfUser);
                    Vector<double> Q_i = Q.Column(indexOfItem);

                    Vector<double> P_u_updated = P_u + (Q_i.Multiply(e_ij) - P_u.Multiply(regularization)).Multiply(learnRate);
                    P.SetRow(indexOfUser, P_u_updated);

                    Vector<double> Q_i_updated = Q_i + (P_u.Multiply(e_ij) - Q_i.Multiply(regularization)).Multiply(learnRate);
                    Q.SetColumn(indexOfItem, Q_i_updated);

                    #region Update feature vectors loop version
                    /*
                    // Update feature vectors
                    for (int k = 0; k < factorCount; ++k)
                    {
                        double factorOfUser = P[indexOfUser, k];
                        double factorOfItem = Q[k, indexOfItem];

                        P[indexOfUser, k] += learnRate * (e_ij * factorOfItem - regularization * factorOfUser);
                        Q[k, indexOfItem] += learnRate * (e_ij * factorOfUser - regularization * factorOfItem);
                    }
                    */
                    #endregion
                }

                // Display the current regularized error see if it converges

                double e_curr = 0;
                if (epoch == 0 || epoch == maxEpoch - 1 || epoch % (int)Math.Ceiling(maxEpoch * 0.1) == 4)
                {
                    Matrix<double> predictedMatrix = R_train_unknown.PointwiseMultiply(P.Multiply(Q));
                    SparseMatrix correctMatrix = R_train.Matrix;
                    double squaredError = (correctMatrix - predictedMatrix).SquaredSum();
                    double regularizationPenaty = regularization * (P.SquaredSum() + Q.SquaredSum());
                    double objective = squaredError + regularizationPenaty;

                    #region Linear implementation
                    /*
                    double e = 0;
                    foreach (Tuple<int, int, double> element in R_train.Ratings)
                    {
                        int indexOfUser = element.Item1;
                        int indexOfItem = element.Item2;
                        double rating = element.Item3;

                        e += Math.Pow(rating - P.Row(indexOfUser).DotProduct(Q.Column(indexOfItem)), 2);

                        for (int k = 0; k < factorCount; ++k)
                        {
                            e += (regularization / 2) * (Math.Pow(P[indexOfUser, k], 2) + Math.Pow(Q[k, indexOfItem], 2));
                        }
                    }
                    */
                    #endregion

                    // Record the current error
                    e_curr = objective;

                    // Stop the learning if the regularized error falls below a certain threshold
                    if (e_prev - e_curr < 0.001)
                    {
                        Console.WriteLine("Improvment less than 0.001, learning stopped.");
                        break;
                    }
                    e_prev = e_curr;

                    Utils.PrintEpoch("Epoch", epoch, maxEpoch, "Objective cost", objective);
                }

            }

            SparseMatrix R_predicted = new SparseMatrix(R_unknown.UserCount, R_unknown.ItemCount);
            foreach(var element in R_unknown.Matrix.EnumerateIndexed(Zeros.AllowSkip))
            {
                int indexOfUser = element.Item1;
                int indexOfItem = element.Item2;
                double r_predicted = meanOfGlobal + P.Row(indexOfUser) * Q.Column(indexOfItem);

                if (r_predicted > Config.Ratings.MaxRating) r_predicted = Config.Ratings.MaxRating;
                if (r_predicted < Config.Ratings.MinRating) r_predicted = Config.Ratings.MinRating;

                R_predicted[indexOfUser, indexOfItem] = r_predicted;
            }
            return new DataMatrix(R_predicted);
            //return new RatingMatrix(R_unknown.PointwiseMultiply(P.Multiply(Q)));
        }
    }
}
