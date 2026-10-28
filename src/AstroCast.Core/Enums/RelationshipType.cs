namespace AstroCast.Core.Enums;

/// <summary>
/// Type of relationship being analyzed in a synastry comparison.
/// Provides essential context for AI interpretation.
/// </summary>
public enum RelationshipType
{
    /// <summary>Not specified.</summary>
    Unspecified = 0,

    /// <summary>Romantic partnership, dating, or marriage.</summary>
    Romantic = 1,

    /// <summary>Married couple.</summary>
    Marriage = 2,

    /// <summary>Parent and child.</summary>
    ParentChild = 3,

    /// <summary>Siblings.</summary>
    Siblings = 4,

    /// <summary>Close friendship.</summary>
    Friendship = 5,

    /// <summary>Business or professional partnership.</summary>
    Business = 6,

    /// <summary>Colleagues or coworkers.</summary>
    Colleagues = 7,

    /// <summary>Teacher and student / mentor and mentee.</summary>
    TeacherStudent = 8,

    /// <summary>Therapist/counselor and client.</summary>
    TherapistClient = 9,

    /// <summary>Adversarial or conflicted relationship.</summary>
    Adversarial = 10,

    /// <summary>Other relationship not listed.</summary>
    Other = 11
}
