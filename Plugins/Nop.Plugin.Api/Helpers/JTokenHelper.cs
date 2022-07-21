using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Nop.Plugin.Api.Helpers
{
    public static class JTokenHelper
    {
        public static JToken RemoveEmptyChildrenAndFilterByFields(this JToken token, IList<string> jsonFields, int level = 1)
        {
            if (token.Type == JTokenType.Object)
            {
                var copy = new JObject();

                foreach (JProperty prop in token.Children<JProperty>())
                {
                    IList<string> jsonFields2 = null;
                    if (jsonFields.Any(Fields => Fields.Contains(prop.Name.ToLowerInvariant())) && level == 3)
                    {
                        foreach (string f in jsonFields)
                        {
                            if(f.Contains(prop.Name))
                            {
                                jsonFields2 = f
                                .Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim())
                                .Distinct()
                                .ToList();
                            }
                        }
                    }
                    JToken child = prop.Value;

                    //if (child.HasValues)
                    //{
                    //    child = child.RemoveEmptyChildrenAndFilterByFields(jsonFields, level + 1);
                    //}

                    // In the current json structure, the first level of properties is level 3. 
                    // If the level is > 3 ( meaning we are not on the first level of properties ), we should not check if the current field is containing into the list with fields, 
                    // so we need to serialize it always.
                    //bool allowedFields = jsonFields.Contains(prop.Name.ToLowerInvariant()) || level > 3;
                    bool allowedFields = jsonFields.Contains(prop.Name.ToLowerInvariant());
                    bool allowedFields2 = jsonFields.Any(Fields => Fields.Contains(prop.Name.ToLowerInvariant()));

                    if (child.HasValues && allowedFields)
                    {
                        child = child.RemoveEmptyChildrenAndFilterByFields(jsonFields, level + 1);
                    }
                    else if(child.HasValues && !allowedFields && allowedFields2)
                    {
                        child = child.RemoveEmptyChildrenAndFilterByFields(jsonFields2, level + 1);
                    }

                    // If the level == 3 ( meaning we are on the first level of properties ), we should not take into account if the current field is values,
                    // so we need to serialize it always.
                    bool notEmpty = !child.IsEmptyOrDefault() || level == 1 || level == 3;

                    if (notEmpty && (allowedFields || allowedFields2))
                    {
                        copy.Add(prop.Name, child);
                    }
                }

                return copy;
            }

            if (token.Type == JTokenType.Array)
            {
                var copy = new JArray();

                foreach (JToken item in token.Children())
                {
                    JToken child = item;

                    if (child.HasValues)
                    {
                        child = child.RemoveEmptyChildrenAndFilterByFields(jsonFields, level + 1);
                    }

                    if (!child.IsEmptyOrDefault())
                    {
                        copy.Add(child);
                    }
                }

                return copy;
            }

            return token;
        }

        private static bool IsEmptyOrDefault(this JToken token)
        {
            return (token.Type == JTokenType.Array && !token.HasValues) || (token.Type == JTokenType.Object && !token.HasValues);
        }
    }
}
