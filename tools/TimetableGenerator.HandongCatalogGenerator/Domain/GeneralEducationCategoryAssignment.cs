using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class GeneralEducationCategoryAssignment
{
    private readonly GeneralEducationCategoryName? mCategoryNameOrNull;

    public static GeneralEducationCategoryAssignment NotProvided { get; } = new GeneralEducationCategoryAssignment(EGeneralEducationCategoryStatus.NotProvided, null);

    public EGeneralEducationCategoryStatus Status { get; }

    public bool HasCategoryName
    {
        get
        {
            return mCategoryNameOrNull != null;
        }
    }

    private GeneralEducationCategoryAssignment(EGeneralEducationCategoryStatus status, GeneralEducationCategoryName? categoryNameOrNull)
    {
        if (Enum.IsDefined(typeof(EGeneralEducationCategoryStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool hasProvidedValue = categoryNameOrNull != null;
        if ((status == EGeneralEducationCategoryStatus.Provided) != hasProvidedValue)
        {
            throw new ArgumentException("Provided general education categories require a name.");
        }

        Status = status;
        mCategoryNameOrNull = categoryNameOrNull;
    }

    public static GeneralEducationCategoryAssignment CreateProvided(GeneralEducationCategoryName categoryName)
    {
        if (categoryName == null)
        {
            throw new ArgumentNullException(nameof(categoryName));
        }

        return new GeneralEducationCategoryAssignment(EGeneralEducationCategoryStatus.Provided, categoryName);
    }

    public GeneralEducationCategoryName GetCategoryName()
    {
        if (mCategoryNameOrNull == null)
        {
            throw new InvalidOperationException("A missing category assignment has no category name.");
        }

        return mCategoryNameOrNull;
    }
}
