using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSay
{
    internal class SavingsAccount
    {
        // Current Interest Rate for this savings account
        public double InterestRate;

        // Current Balance of Account
        public double Balance;

        // History of transcations
        public Dictionary<int, Transaction> TransactionHistory;
    }
}
