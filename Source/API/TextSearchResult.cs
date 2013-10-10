using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.API
{
    /// <summary>
    /// A holder object to represent the score from a text search and the matching document
    /// </summary>
    /// <typeparam name="T">The type of the document that was matched</typeparam>
    public class TextSearchResult<T>
    {
        /// <summary>
        /// The relevance score from MongoDB from a text search query
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// The result document
        /// </summary>
        public T Result { get; set; }
    }
}
