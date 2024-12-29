using Newtonsoft.Json;
using System;

namespace Cosmos.Copilot.Models
{
    /// <summary>
    /// Represents a knowledge base item.
    /// </summary>
    public class KnowledgeBaseItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("categoryId")]
        public string CategoryId { get; set; }

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
        /// Default constructor for deserialization.
        /// </summary>
        [JsonConstructor]
        private KnowledgeBaseItem() { }

        /// <summary>
        /// Constructor for creating a new knowledge base item with a unique Id.
        /// </summary>
        /// <param name="uniqueKey">The unique identifier for the knowledge base item.</param>
        /// <param name="tenantId">The tenant ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <param name="category">The category of the item.</param>
        /// <param name="title">The title of the item.</param>
        /// <param name="content">The content of the item.</param>
        /// <param name="referenceDescription">Description for referencing the item.</param>
        /// <param name="referenceLink">Link for referencing the item.</param>
        /// <param name="vectors">Embedding vectors for the item.</param>
        /// <exception cref="ArgumentException">Thrown if any required parameter is null or empty.</exception>
        public KnowledgeBaseItem(
            string uniqueKey,
            string tenantId,
            string userId,
            string categoryId,
            string title,
            string content,
            string referenceDescription,
            string referenceLink,
            float[] vectors)
        {
            if (string.IsNullOrWhiteSpace(uniqueKey)) throw new ArgumentException("UniqueKey cannot be null or empty.", nameof(uniqueKey));
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("TenantId cannot be null or empty.", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("UserId cannot be null or empty.", nameof(userId));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category cannot be null or empty.", nameof(categoryId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be null or empty.", nameof(title));
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content cannot be null or empty.", nameof(content));

            Id = uniqueKey;
            Type = nameof(KnowledgeBaseItem);
            TenantId = tenantId;
            UserId = userId;
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
        public KnowledgeBaseItem GetSanitizedCopy()
        {
            return new KnowledgeBaseItem(
                uniqueKey: Id,
                tenantId: "SANITIZED_TENANT_ID",
                userId: "SANITIZED_USER_ID",
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
