using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class InstitutionMetadata
{
    public InstitutionId Id { get; }

    public InstitutionName KoreanName { get; }

    public EnglishInstitutionName EnglishName { get; }

    public InstitutionMetadata(InstitutionId id, InstitutionName koreanName, EnglishInstitutionName englishName)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (koreanName == null)
        {
            throw new ArgumentNullException(nameof(koreanName));
        }

        if (englishName == null)
        {
            throw new ArgumentNullException(nameof(englishName));
        }

        Id = id;
        KoreanName = koreanName;
        EnglishName = englishName;
    }
}
