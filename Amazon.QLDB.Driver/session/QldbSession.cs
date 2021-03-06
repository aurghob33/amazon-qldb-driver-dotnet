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
    using System.Net;
    using System.Threading;
    using Amazon.IonDotnet.Tree;
    using Amazon.QLDBSession;
    using Amazon.QLDBSession.Model;
    using Amazon.Runtime;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// <para>Represents a session to a specific ledger within QLDB, allowing for execution of PartiQL statements and
    /// retrieval of the associated results, along with control over transactions for bundling multiple executions.</para>
    ///
    /// <para>The execute methods provided will automatically retry themselves in the case that an unexpected recoverable error
    /// occurs, including OCC conflicts, by starting a brand new transaction and re-executing the statement within the new
    /// transaction.</para>
    ///
    /// <para>There are three methods of execution, ranging from simple to complex; the first two are recommended for their inbuilt
    /// error handling:
    /// <list type="bullet">
    /// <item><description>Execute(string, Action), Execute(string, Action, List), and Execute(string, Action, params IIonValue[])
    /// allow for a single statement to be executed within a transaction where the transaction is implicitly created
    /// and committed, and any recoverable errors are transparently handled. Each parameter besides the statement string
    /// have overloaded method variants where they are not necessary.</description></item>
    /// <item><description>Execute(Action, Action) and Execute(Func, Action) allow for more complex execution sequences where
    /// more than one execution can occur, as well as other method calls. The transaction is implicitly created and committed, and any
    /// recoverable errors are transparently handled. The second Action parameter has overloaded variants where it is not
    /// necessary.</description></item>
    /// <item><description><see cref="StartTransaction"/> allows for full control over when the transaction is committed and leaves the
    /// responsibility of OCC conflict handling up to the user. Transaction methods cannot be automatically retried, as
    /// the state of the transaction is ambiguous in the case of an unexpected error.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal class QldbSession : IQldbSession
    {
        private readonly int retryLimit;
        private readonly ILogger logger;
        private Session session;
        private bool isClosed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="QldbSession"/> class.
        /// </summary>
        ///
        /// <param name="session">The session object representing a communication channel with QLDB.</param>
        /// <param name="retryLimit">The limit for retries on execute methods when an OCC conflict or retriable exception occurs.</param>
        /// <param name="logger">The logger to be used by this.</param>
        internal QldbSession(Session session, int retryLimit, ILogger logger)
        {
            this.session = session;
            this.retryLimit = retryLimit;
            this.logger = logger;
        }

        /// <summary>
        /// End this session. No-op if already closed.
        /// </summary>
        public void Dispose()
        {
            if (!this.isClosed)
            {
                this.isClosed = true;
                this.session.End();
            }
        }

        /// <summary>
        /// Execute the statement against QLDB and retrieve the result.
        /// </summary>
        ///
        /// <param name="statement">The PartiQL statement to be executed against QLDB.</param>
        ///
        /// <returns>The result of executing the statement.</returns>
        ///
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        public virtual IResult Execute(string statement)
        {
            return this.Execute(txn => { return txn.Execute(statement); }, null);
        }

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
        public virtual IResult Execute(string statement, Action<int> retryAction)
        {
            return this.Execute(txn => { return txn.Execute(statement); }, retryAction);
        }

        /// <summary>
        /// Execute the statement using the specified parameters against QLDB and retrieve the result.
        /// </summary>
        ///
        /// <param name="statement">The PartiQL statement to be executed against QLDB.</param>
        /// <param name="parameters">Parameters to execute.</param>
        ///
        /// <returns>The result of executing the statement.</returns>
        ///
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        public virtual IResult Execute(string statement, List<IIonValue> parameters)
        {
            return this.Execute(txn => { return txn.Execute(statement, parameters); }, null);
        }

        /// <summary>
        /// Execute the statement using the specified parameters against QLDB and retrieve the result.
        /// </summary>
        ///
        /// <param name="statement">The PartiQL statement to be executed against QLDB.</param>
        /// <param name="parameters">Parameters to execute.</param>
        ///
        /// <returns>The result of executing the statement.</returns>
        ///
        /// <exception cref="ObjectDisposedException">Thrown when called on a disposed instance.</exception>
        /// <exception cref="AmazonClientException">Thrown when there is an error executing against QLDB.</exception>
        public virtual IResult Execute(string statement, params IIonValue[] parameters)
        {
            return this.Execute(txn => { return txn.Execute(statement, parameters); }, null);
        }

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
        public virtual IResult Execute(string statement, Action<int> retryAction, List<IIonValue> parameters)
        {
            return this.Execute(txn => { return txn.Execute(statement, parameters); }, retryAction);
        }

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
        public virtual IResult Execute(string statement, Action<int> retryAction, params IIonValue[] parameters)
        {
            return this.Execute(txn => { return txn.Execute(statement, parameters); }, retryAction);
        }

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
        public virtual void Execute(Action<TransactionExecutor> action)
        {
            this.Execute(action, null);
        }

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
        public virtual void Execute(Action<TransactionExecutor> action, Action<int> retryAction)
        {
            ValidationUtils.AssertNotNull(action, "action");
            this.Execute(
                txn =>
                {
                    action(txn);
                    return true;
                }, retryAction);
        }

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
        public virtual T Execute<T>(Func<TransactionExecutor, T> func)
        {
            return this.Execute(func, null);
        }

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
        public virtual T Execute<T>(Func<TransactionExecutor, T> func, Action<int> retryAction)
        {
            ValidationUtils.AssertNotNull(func, "func");
            this.ThrowIfClosed();

            var executionAttempt = 0;
            while (true)
            {
                ITransaction transaction = null;
                try
                {
                    transaction = this.StartTransaction();
                    T returnedValue = func(new TransactionExecutor(transaction));
                    if (returnedValue is IResult)
                    {
                        returnedValue = (T)(object)BufferedResult.BufferResult((IResult)returnedValue);
                    }

                    transaction.Commit();
                    return returnedValue;
                }
                catch (AbortException ae)
                {
                    this.NoThrowAbort(transaction);
                    throw ae;
                }
                catch (InvalidSessionException ise)
                {
                    if (executionAttempt >= this.retryLimit)
                    {
                        throw ise;
                    }

                    this.logger.LogInformation(
                        "Creating a new session to QLDB; previous session is no longer valid: {}", ise);
                    this.session =
                        Session.StartSession(this.session.LedgerName, this.session.SessionClient, this.logger);
                }
                catch (OccConflictException oce)
                {
                    this.logger.LogInformation("OCC conflict occured: {}", oce);
                    if (executionAttempt >= this.retryLimit)
                    {
                        throw oce;
                    }
                }
                catch (AmazonQLDBSessionException aqse)
                {
                    this.NoThrowAbort(transaction);
                    if (executionAttempt >= this.retryLimit ||
                        ((aqse.StatusCode != HttpStatusCode.InternalServerError) &&
                        (aqse.StatusCode != HttpStatusCode.ServiceUnavailable)))
                    {
                        throw aqse;
                    }
                }
                catch (AmazonClientException ace)
                {
                    this.NoThrowAbort(transaction);
                    throw ace;
                }

                executionAttempt++;
                retryAction?.Invoke(executionAttempt);
                SleepOnRetry(executionAttempt);
            }
        }

        /// <summary>
        /// Retrieve the table names that are available within the ledger.
        /// </summary>
        ///
        /// <returns>The Enumerable over the table names in the ledger.</returns>
        public virtual IEnumerable<string> ListTableNames()
        {
            const string TableNameQuery =
                "SELECT VALUE name FROM information_schema.user_tables WHERE status = 'ACTIVE'";
            var result = this.Execute(TableNameQuery);
            var tableNames = new List<string>();
            foreach (var ionValue in result)
            {
                tableNames.Add(ionValue.StringValue);
            }

            return tableNames;
        }

        /// <summary>
        /// Create a transaction object which allows for granular control over when a transaction is aborted or committed.
        /// </summary>
        ///
        /// <returns>The newly created transaction object.</returns>
        public virtual ITransaction StartTransaction()
        {
            this.ThrowIfClosed();

            var startTransactionResult = this.session.StartTransaction();
            return new Transaction(this.session, startTransactionResult.TransactionId, this.logger);
        }

        /// <summary>
        /// Determine if the session is alive by sending an abort message. Will close the session if the abort is
        /// unsuccessful. This should only be used when the session is known to not be in use, otherwise the state
        /// will be abandoned.
        /// </summary>
        ///
        /// <returns>True if the abort transaction was successful, else false.</returns>
        internal bool AbortOrClose()
        {
            if (this.isClosed)
            {
                return false;
            }

            try
            {
                this.session.AbortTransaction();
                return true;
            }
            catch (AmazonClientException)
            {
                this.isClosed = true;
                return false;
            }
        }

        /// <summary>
        /// Retrieve the ID of this session..
        /// </summary>
        ///
        /// <returns>The ID of this session.</returns>
        internal string GetSessionId()
        {
            return this.session.SessionId;
        }

        /// <summary>
        /// Sleep for an exponentially increasing amount relative to executionAttempt.
        /// </summary>
        ///
        /// <param name="executionAttempt">The number of execution attempts.</param>
        private static void SleepOnRetry(int executionAttempt)
        {
            const int SleepBaseMilliseconds = 10;
            const int SleepCapMilliseconds = 5000;
            var rng = new Random();
            var jitterRand = rng.NextDouble();
            var exponentialBackoff = Math.Min(SleepCapMilliseconds, Math.Pow(SleepBaseMilliseconds, executionAttempt));
            Thread.Sleep(Convert.ToInt32(jitterRand * (exponentialBackoff + 1)));
        }

        /// <summary>
        /// Send an abort which will not throw on failure.
        /// </summary>
        ///
        /// <param name="transaction">The transaction to abort.</param>
        ///
        /// <exception cref="AmazonServiceException">If there is an error communicating with QLDB.</exception>
        private void NoThrowAbort(ITransaction transaction)
        {
            try
            {
                if (transaction != null)
                {
                    transaction.Abort();
                }
                else
                {
                    this.session.AbortTransaction();
                }
            }
            catch (AmazonServiceException ase)
            {
                this.logger.LogWarning("Ignored error aborting transaction during execution: {}", ase);
            }
        }

        /// <summary>
        /// If the session is closed throw an <see cref="ObjectDisposedException"/>.
        /// </summary>
        ///
        /// <exception cref="ObjectDisposedException">Exception when the session is already closed.</exception>
        private void ThrowIfClosed()
        {
            if (this.isClosed)
            {
                throw new ObjectDisposedException(ExceptionMessages.SessionClosed);
            }
        }
    }
}
