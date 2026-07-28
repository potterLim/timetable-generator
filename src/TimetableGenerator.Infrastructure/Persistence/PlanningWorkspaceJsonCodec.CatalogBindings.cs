using System.Collections.Generic;
using System.Text.Json;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private static PlanCatalogBinding readCatalogBinding(JsonElement element, string context)
    {
        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            context,
            new string[]
            {
                "catalogId",
                "institutionId",
                "term",
                "revision",
                "artifactSha256",
            });
        CatalogId catalogId = new CatalogId(readString(properties["catalogId"], context + ".catalogId"));
        InstitutionId institutionId = new InstitutionId(readString(properties["institutionId"], context + ".institutionId"));
        AcademicTerm term = AcademicTerm.Parse(readString(properties["term"], context + ".term"));
        CatalogRevision revision = new CatalogRevision(readInt32(properties["revision"], context + ".revision"));
        CatalogArtifactSha256 artifactSha256 = new CatalogArtifactSha256(readString(properties["artifactSha256"], context + ".artifactSha256"));
        return new PlanCatalogBinding(catalogId, institutionId, term, revision, artifactSha256);
    }

    private static void writeCatalogBinding(Utf8JsonWriter writer, string propertyName, PlanCatalogBinding catalogBinding)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteString("catalogId", catalogBinding.CatalogId.Value);
        writer.WriteString("institutionId", catalogBinding.InstitutionId.Value);
        writer.WriteString("term", catalogBinding.Term.Id);
        writer.WriteNumber("revision", catalogBinding.Revision.Value);
        writer.WriteString("artifactSha256", catalogBinding.ArtifactSha256.HexValue);
        writer.WriteEndObject();
    }
}
