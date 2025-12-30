using System.Text;

namespace EveDataCollator
{
    /// <summary>
    /// Helper class for parsing JSONL files
    /// </summary>
    public static class JsonlHelper
    {
        /// <summary>
        /// Read a complete JSON object from a stream, handling multi-line objects
        /// </summary>
        /// <param name="reader">The stream reader</param>
        /// <returns>The complete JSON string, or null if end of stream</returns>
        public static string? ReadCompleteJsonObject(StreamReader reader)
        {
            if(reader.EndOfStream)
            {
                return null;
            }

            var jsonBuilder = new StringBuilder();
            int braceDepth = 0;
            bool inString = false;
            bool escapeNext = false;

            while(!reader.EndOfStream)
            {
                char c = (char)reader.Read();

                jsonBuilder.Append(c);

                if(escapeNext)
                {
                    escapeNext = false;
                    continue;
                }

                if(c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if(c == '"' && !escapeNext)
                {
                    inString = !inString;
                    continue;
                }

                if(!inString)
                {
                    if(c == '{')
                    {
                        braceDepth++;
                    }
                    else if(c == '}')
                    {
                        braceDepth--;
                        if(braceDepth == 0)
                        {
                            // Complete JSON object found
                            return jsonBuilder.ToString();
                        }
                    }
                }
            }

            // End of stream reached
            return braceDepth == 0 && jsonBuilder.Length > 0 ? jsonBuilder.ToString() : null;
        }
    }
}

