﻿/*
 * Copyright 2020 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"). You may not use this file except in compliance with
 * the License. A copy of the License is located at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * or in the "license" file accompanying this file. This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

namespace Amazon.QLDB.Driver
{
    using System;
    using System.Collections.Generic;
    using Amazon.IonDotnet.Tree;
    using Amazon.Runtime;

    /// <summary>
    /// Interface for execution against QLDB, that may be retried in the case of a non-fatal error.
    /// </summary>
    public interface IRetriableExecutable : IExecutable
    {
        /// <summary>
        /// Execute the statement against QLDB and retrieve the result.
        /// </summary>
        ///
        /// <param name="statement">The PartiQL statement to be executed against QLDB.</param>
        /// <param name="retryAction">A lambda that is invoked when the statement execution is about to be retried due to
        /// a retriable error. Can be null if not applicable.</param>
        ///
        /// <returns>The result of executing the statement.</returns>
        ///
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        IResult Execute(string statement, Action<int> retryAction);

        /// <summary>
        /// Execute the statement using the specified parameters against QLDB and retrieve the result.
        /// </summary>
        ///
        /// <param name="statement">The PartiQL statement to be executed against QLDB.</param>
        /// <param name="retryAction">A lambda that is invoked when the statement execution is about to be retried due to
        /// a retriable error. Can be null if not applicable.</param>
        /// <param name="parameters">Parameters to execute.</param>
        ///
        /// <returns>The result of executing the statement.</returns>
        ///
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        IResult Execute(string statement, Action<int> retryAction, List<IIonValue> parameters);

        /// <summary>
        /// Execute the statement using the specified parameters against QLDB and retrieve the result.
        /// </summary>
        ///
        /// <param name="statement">The PartiQL statement to be executed against QLDB.</param>
        /// <param name="retryAction">A lambda that is invoked when the statement execution is about to be retried due to
        /// a retriable error. Can be null if not applicable.</param>
        /// <param name="parameters">Parameters to execute.</param>
        ///
        /// <returns>The result of executing the statement.</returns>
        ///
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        IResult Execute(string statement, Action<int> retryAction, params IIonValue[] parameters);

        /// <summary>
        /// Execute the Executor lambda against QLDB within a transaction where no result is expected.
        /// </summary>
        ///
        /// <param name="action">The Executor lambda with no return value representing the block of code to be executed within the transaction.
        /// This cannot have any side effects as it may be invoked multiple times.</param>
        ///
        /// <exception cref="AbortException">Thrown if the Executor lambda calls <see cref="TransactionExecutor.Abort"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        void Execute(Action<TransactionExecutor> action);

        /// <summary>
        /// Execute the Executor lambda against QLDB within a transaction where no result is expected.
        /// </summary>
        ///
        /// <param name="action">The Executor lambda with no return value representing the block of code to be executed within the transaction.
        /// This cannot have any side effects as it may be invoked multiple times.</param>
        /// <param name="retryAction">A lambda that is invoked when the Executor lambda is about to be retried due to
        /// a retriable error. Can be null if not applicable.</param>
        ///
        /// <exception cref="AbortException">Thrown if the Executor lambda calls <see cref="TransactionExecutor.Abort"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        void Execute(Action<TransactionExecutor> action, Action<int> retryAction);

        /// <summary>
        /// Execute the Executor lambda against QLDB and retrieve the result within a transaction.
        /// </summary>
        ///
        /// <param name="func">The Executor lambda representing the block of code to be executed within the transaction. This cannot have any
        /// side effects as it may be invoked multiple times, and the result cannot be trusted until the
        /// transaction is committed.</param>
        /// <typeparam name="T">The return type.</typeparam>
        ///
        /// <returns>The return value of executing the executor. Note that if you directly return a <see cref="IResult"/>, this will
        /// be automatically buffered in memory before the implicit commit to allow reading, as the commit will close
        /// any open results. Any other <see cref="IResult"/> instances created within the executor block will be
        /// invalidated, including if the return value is an object which nests said <see cref="IResult"/> instances within it.
        /// </returns>
        ///
        /// <exception cref="AbortException">Thrown if the Executor lambda calls <see cref="TransactionExecutor.Abort"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        T Execute<T>(Func<TransactionExecutor, T> func);

        /// <summary>
        /// Execute the Executor lambda against QLDB and retrieve the result within a transaction.
        /// </summary>
        ///
        /// <param name="func">The Executor lambda representing the block of code to be executed within the transaction. This cannot have any
        /// side effects as it may be invoked multiple times, and the result cannot be trusted until the
        /// transaction is committed.</param>
        /// <param name="retryAction">A lambda that is invoked when the Executor lambda is about to be retried due to
        /// a retriable error. Can be null if not applicable.</param>
        /// <typeparam name="T">The return type.</typeparam>
        ///
        /// <returns>The return value of executing the executor. Note that if you directly return a <see cref="IResult"/>, this will
        /// be automatically buffered in memory before the implicit commit to allow reading, as the commit will close
        /// any open results. Any other <see cref="IResult"/> instances created within the executor block will be
        /// invalidated, including if the return value is an object which nests said <see cref="IResult"/> instances within it.
        /// </returns>
        ///
        /// <exception cref="AbortException">Thrown if the Executor lambda calls <see cref="TransactionExecutor.Abort"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        T Execute<T>(Func<TransactionExecutor, T> func, Action<int> retryAction);
    }
}
