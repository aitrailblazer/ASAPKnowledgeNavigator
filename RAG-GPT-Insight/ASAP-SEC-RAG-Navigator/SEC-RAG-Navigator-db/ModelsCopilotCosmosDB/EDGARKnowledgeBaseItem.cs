using Newtonsoft.Json;
using System;

namespace Cosmos.Copilot.Models
{
    /// <summary>
    /// Represents a knowledge base item.
    /// </summary>
    public class EDGARKnowledgeBaseItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("form")]
        public string Form { get; set; }

        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; }

        [JsonProperty("partitionKey")]
        public string PartitionKey => $"{Form}_{Ticker}_{CategoryId}";

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("referenceDescription")]
        public string ReferenceDescription { get; set; }

        [JsonProperty("referenceLink")]
        public string ReferenceLink { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonProperty("similarityScore")]
        public double SimilarityScore { get; set; }

        [JsonProperty("relevanceScore")]
        public double RelevanceScore { get; set; }

        [JsonProperty("vectors")]
        public float[] Vectors { get; set; }

        /// <summary>
        /// Public default constructor for deserialization and general usage.
        /// </summary>
        public EDGARKnowledgeBaseItem()
        {
            Id = Guid.NewGuid().ToString();
            Type = nameof(EDGARKnowledgeBaseItem);
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Vectors = Array.Empty<float>();
        }

        /// <summary>
        /// Constructor for creating a new knowledge base item with a unique Id.
        /// </summary>
        /// <param name="uniqueKey">The unique identifier for the knowledge base item.</param>
        /// <param name="form">The tenant ID.</param>
        /// <param name="ticker">The user ID.</param>
        /// <param name="categoryId">The category of the item.</param>
        /// <param name="title">The title of the item.</param>
        /// <param name="content">The content of the item.</param>
        /// <param name="referenceDescription">Description for referencing the item.</param>
        /// <param name="referenceLink">Link for referencing the item.</param>
        /// <param name="vectors">Embedding vectors for the item.</param>
        /// <exception cref="ArgumentException">Thrown if any required parameter is null or empty.</exception>
        public EDGARKnowledgeBaseItem(
            string uniqueKey,
            string form,
            string ticker,
            string categoryId,
            string title,
            string content,
            string referenceDescription,
            string referenceLink,
            float[] vectors)
        {
            if (string.IsNullOrWhiteSpace(uniqueKey)) throw new ArgumentException("UniqueKey cannot be null or empty.", nameof(uniqueKey));
            if (string.IsNullOrWhiteSpace(form)) throw new ArgumentException("form cannot be null or empty.", nameof(form));
            if (string.IsNullOrWhiteSpace(ticker)) throw new ArgumentException("ticker cannot be null or empty.", nameof(ticker));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("CategoryId cannot be null or empty.", nameof(categoryId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be null or empty.", nameof(title));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content cannot be null or empty.", nameof(content));

            Id = uniqueKey;
            Type = nameof(EDGARKnowledgeBaseItem);
            Form = form;
            Ticker = ticker;
            CategoryId = categoryId;
            Title = title;
            Content = content;
            ReferenceDescription = referenceDescription;
            ReferenceLink = referenceLink;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Vectors = vectors ?? Array.Empty<float>();
        }

        /// <summary>
        /// Updates the content of the knowledge base item and refreshes the updated timestamp.
        /// </summary>
        /// <param name="newContent">The new content for the item.</param>
        /// <param name="newReferenceDescription">Optional new reference description.</param>
        /// <param name="newReferenceLink">Optional new reference link.</param>
        /// <exception cref="ArgumentException">Thrown if newContent is null or empty.</exception>
        public void UpdateContent(string newContent, string newReferenceDescription = null, string newReferenceLink = null)
        {
            if (string.IsNullOrWhiteSpace(newContent))
                throw new ArgumentException("Content cannot be null or empty.", nameof(newContent));

            Content = newContent;
            ReferenceDescription = newReferenceDescription ?? ReferenceDescription;
            ReferenceLink = newReferenceLink ?? ReferenceLink;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a sanitized copy of the knowledge base item with tenant and user IDs replaced.
        /// </summary>
        /// <returns>A sanitized copy of the knowledge base item.</returns>
        public EDGARKnowledgeBaseItem GetSanitizedCopy()
        {
            return new EDGARKnowledgeBaseItem(
                uniqueKey: Id,
                form: "SANITIZED_FORM_ID",
                ticker: "SANITIZED_TICKER_ID",
                categoryId: CategoryId,
                title: Title,
                content: Content,
                referenceDescription: ReferenceDescription,
                referenceLink: ReferenceLink,
                vectors: Vectors
            );
        }
    }
}
