﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace Microsoft.Xrm.DevOps.Data
{
    public class BuilderEntityMetadata
    {
        public EntityMetadata Metadata { get; set; }
        public EntityMetadata PartyMetadata { get; set; }
        public List<String> Attributes { get; private set; }
        public List<String> Identifiers = new List<String>();
        public Boolean PluginsDisabled = false;
        public Queue<Entity> Entities { get; set; }
        public Dictionary<String, Dictionary<Guid, List<Guid>>> RelatedEntities = new Dictionary<String, Dictionary<Guid, List<Guid>>>();

        public BuilderEntityMetadata()
        {
            Entities = new Queue<Entity>();
            Attributes = new List<string>();
        }

        public void AppendEntity(Entity entity)
        {
            Attributes = entity.Attributes.Select(b => b.Key)
                        .Union(Attributes)
                        .Distinct()
                        .ToList<String>();

            Entities.Enqueue(entity);
        }

        public void AppendEntities(List<Entity> entities)
        {
            entities.ForEach(entity => AppendEntity(entity));
        }

        public void AppendM2MDataToEntity(String relationshipName, Dictionary<Guid, List<Guid>> relatedEntities)
        {
            if (!RelatedEntities.ContainsKey(relationshipName))
                RelatedEntities[relationshipName] = new Dictionary<Guid, List<Guid>>();

            foreach (var id in relatedEntities.Keys)
            {
                if (RelatedEntities[relationshipName].ContainsKey(id))
                    RelatedEntities[relationshipName][id].AddRange(relatedEntities[id]);
                else
                    RelatedEntities[relationshipName][id] = relatedEntities[id];

                RelatedEntities[relationshipName][id] = RelatedEntities[relationshipName][id].Distinct().ToList();
            }
        }

        public void CommitIdentifier()
        {
            // Default to the Guid as the identifier
            if (Identifiers.Count == 0)
            {
                Identifiers.Add(Metadata.PrimaryIdAttribute);
            }

            // Add attribute matching the primary ID if it wasn't provided
            if (Identifiers.Contains(Metadata.PrimaryIdAttribute))
            {
                foreach (var record in this.Entities)
                {
                    if (!String.IsNullOrEmpty(record.Id.ToString()))
                        record[Metadata.PrimaryIdAttribute] = record.Id;
                }
            }

            // Calculate what records exist when the identifier is enforced
            Dictionary<String, Entity> DistinctEntities = new Dictionary<String, Entity>();
            
            while (Entities.Count > 0)
            {
                var entity = Entities.Dequeue();
                String EntityIdentifier = GetIdentifierFromEntity(entity);
                if (DistinctEntities.ContainsKey(EntityIdentifier))
                {
                    Entity priorEntity = DistinctEntities[EntityIdentifier];
                    foreach (var attribute in entity.Attributes)
                    {
                        priorEntity[attribute.Key] = attribute.Value;
                    }
                    DistinctEntities[EntityIdentifier] = priorEntity;
                } else {
                    DistinctEntities.Add(EntityIdentifier, entity);
                }
            }

            // Rebuild list of entities based on an enforced identifier
            DistinctEntities.Keys.ToList<String>().ForEach(key => Entities.Enqueue(DistinctEntities[key]));
        }

        private String GetIdentifierFromEntity(Entity entity)
        {
            List<Object> EntityIdentifier = new List<Object>();

            Identifiers.ForEach(identifier =>
            {
                if (entity.Contains(identifier))
                {
                    EntityIdentifier.Add(entity[identifier]);
                }
            });

            return String.Join("|", EntityIdentifier);
        }
    }
}
