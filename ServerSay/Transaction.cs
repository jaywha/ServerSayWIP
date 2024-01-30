using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSay
{
    internal class Transaction
    {
        // The Id of the Transaction
        public int Id;

        // The value of the transaction
        public double Amount;

        // Types of transactions
        public TransactionType Type;
    }

    internal enum TransactionType
    {
        // Savings
        Deposit = 0,
        Withdraw = 1,
        Interest = 2
        // Note: Checking/Other?
    }
}
