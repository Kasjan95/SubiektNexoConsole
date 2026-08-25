using InsERT.Moria.Sfera;
using System.Text;

namespace SubiektNexoConnector.Infrastructure.Nexo.Common
{
    internal static class NexoValidationMessageBuilder
    {
        public static string Build(string messagePrefix, IEnumerable<KomunikatWalidacji> errors)
        {
            StringBuilder messageBuilder = new(messagePrefix);

            foreach (var error in errors)
            {
                var fieldNames = error.NazwyPol is null || !error.NazwyPol.Any()
                    ? "Unknown field"
                    : string.Join(", ", error.NazwyPol);

                messageBuilder.AppendLine();
                messageBuilder.Append($"{fieldNames}: {error.Tresc}");
            }

            return messageBuilder.ToString();
        }
    }
}
