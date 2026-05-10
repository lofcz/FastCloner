using FastCloner.Code;

namespace FastCloner.Tests;

public class VisibilityAttributeRuntimeTests
{
    #region Test types - public-only policy

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class PublicOnlyDto
    {
        public int Public;
        internal int Internal;
        protected int Protected;
        private int _private;

        public void Set(int p, int i, int pr, int priv)
        {
            Public = p;
            Internal = i;
            Protected = pr;
            _private = priv;
        }

        public int GetPrivate() => _private;
        public int GetProtected() => Protected;
    }

    #endregion

    [Test]
    public async Task PublicOnly_policy_clones_only_public_members()
    {
        PublicOnlyDto src = new PublicOnlyDto();
        src.Set(p: 100, i: 200, pr: 300, priv: 400);

        PublicOnlyDto clone = src.DeepClone();

        await Assert.That(clone.Public).IsEqualTo(100);
        await Assert.That(clone.Internal).IsEqualTo(0);
        await Assert.That(clone.GetProtected()).IsEqualTo(0);
        await Assert.That(clone.GetPrivate()).IsEqualTo(0);
    }

    #region Test types - public + internal policy

    [FastClonerVisibility(FastClonerMemberVisibility.Public | FastClonerMemberVisibility.Internal)]
    public class PublicAndInternalDto
    {
        public int Public;
        internal int Internal;
        protected int Protected;
        private int _private;

        public void Set(int p, int i, int pr, int priv)
        {
            Public = p;
            Internal = i;
            Protected = pr;
            _private = priv;
        }

        public int GetPrivate() => _private;
        public int GetProtected() => Protected;
    }

    #endregion

    [Test]
    public async Task PublicAndInternal_policy_includes_internal_excludes_protected_and_private()
    {
        PublicAndInternalDto src = new PublicAndInternalDto();
        src.Set(p: 1, i: 2, pr: 3, priv: 4);

        PublicAndInternalDto clone = src.DeepClone();

        await Assert.That(clone.Public).IsEqualTo(1);
        await Assert.That(clone.Internal).IsEqualTo(2);
        await Assert.That(clone.GetProtected()).IsEqualTo(0);
        await Assert.That(clone.GetPrivate()).IsEqualTo(0);
    }

    #region Test types - explicit member behavior overrides policy

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class MemberLevelOverridesPolicy
    {
        public int Public;

        // Type-level policy excludes private; member-level explicit Clone behavior forces it back in.
        [FastClonerBehavior(CloneBehavior.Clone)]
        private int _private;

        public void Set(int p, int priv)
        {
            Public = p;
            _private = priv;
        }

        public int GetPrivate() => _private;
    }

    #endregion

    [Test]
    public async Task MemberLevel_FastClonerBehavior_Clone_overrides_visibility_policy()
    {
        MemberLevelOverridesPolicy src = new MemberLevelOverridesPolicy();
        src.Set(p: 10, priv: 99);

        MemberLevelOverridesPolicy clone = src.DeepClone();

        await Assert.That(clone.Public).IsEqualTo(10);
        await Assert.That(clone.GetPrivate()).IsEqualTo(99);
    }

    #region Test types - ignore beats All policy

    [FastClonerVisibility(FastClonerMemberVisibility.All)]
    public class IgnoreBeatsAllPolicy
    {
        public int Public;

        [FastClonerIgnore]
        private int _ignored;

        public void Set(int p, int ig)
        {
            Public = p;
            _ignored = ig;
        }

        public int GetIgnored() => _ignored;
    }

    #endregion

    [Test]
    public async Task FastClonerIgnore_overrides_All_visibility_policy()
    {
        IgnoreBeatsAllPolicy src = new IgnoreBeatsAllPolicy();
        src.Set(p: 7, ig: 999);

        IgnoreBeatsAllPolicy clone = src.DeepClone();

        await Assert.That(clone.Public).IsEqualTo(7);
        await Assert.That(clone.GetIgnored()).IsEqualTo(0);
    }

    #region Test types - default (no attribute) clones everything

    public class NoVisibilityAttributeDto
    {
        public int Public;
        internal int Internal;
        protected int Protected;
        private int _private;

        public void Set(int p, int i, int pr, int priv)
        {
            Public = p;
            Internal = i;
            Protected = pr;
            _private = priv;
        }

        public int GetPrivate() => _private;
        public int GetProtected() => Protected;
    }

    #endregion

    [Test]
    public async Task No_attribute_means_All_policy_clones_everything()
    {
        NoVisibilityAttributeDto src = new NoVisibilityAttributeDto();
        src.Set(p: 1, i: 2, pr: 3, priv: 4);

        NoVisibilityAttributeDto clone = src.DeepClone();

        await Assert.That(clone.Public).IsEqualTo(1);
        await Assert.That(clone.Internal).IsEqualTo(2);
        await Assert.That(clone.GetProtected()).IsEqualTo(3);
        await Assert.That(clone.GetPrivate()).IsEqualTo(4);
    }

    #region Test types - inheritance + policy on derived

    public class BaseWithMembers
    {
        public int BasePublic;
        private int _basePrivate;

        public void SetBase(int p, int priv)
        {
            BasePublic = p;
            _basePrivate = priv;
        }

        public int GetBasePrivate() => _basePrivate;
    }

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class DerivedWithPublicOnlyPolicy : BaseWithMembers
    {
        public int DerivedPublic;
        private int _derivedPrivate;

        public void SetDerived(int p, int priv)
        {
            DerivedPublic = p;
            _derivedPrivate = priv;
        }

        public int GetDerivedPrivate() => _derivedPrivate;
    }

    #endregion

    [Test]
    public async Task Policy_on_derived_type_filters_inherited_base_members()
    {
        DerivedWithPublicOnlyPolicy src = new DerivedWithPublicOnlyPolicy();
        src.SetBase(p: 11, priv: 22);
        src.SetDerived(p: 33, priv: 44);

        DerivedWithPublicOnlyPolicy clone = src.DeepClone();

        await Assert.That(clone.BasePublic).IsEqualTo(11);
        await Assert.That(clone.GetBasePrivate()).IsEqualTo(0);
        await Assert.That(clone.DerivedPublic).IsEqualTo(33);
        await Assert.That(clone.GetDerivedPrivate()).IsEqualTo(0);
    }

    #region Test types - cache stability

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class CacheStabilityDto
    {
        public int Public;
        private int _private;

        public void Set(int p, int priv)
        {
            Public = p;
            _private = priv;
        }

        public int GetPrivate() => _private;
    }

    #endregion

    [Test]
    public async Task Visibility_policy_is_stable_across_repeated_clones()
    {
        CacheStabilityDto src = new CacheStabilityDto();
        src.Set(p: 5, priv: 6);

        for (int i = 0; i < 3; i++)
        {
            CacheStabilityDto clone = src.DeepClone();
            await Assert.That(clone.Public).IsEqualTo(5);
            await Assert.That(clone.GetPrivate()).IsEqualTo(0);
        }
    }

    #region Test types - inherited policy attribute

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class BaseWithVisibilityPolicy
    {
        public int BasePublic;
        private int _basePrivate;

        public void SetBase(int p, int priv)
        {
            BasePublic = p;
            _basePrivate = priv;
        }

        public int GetBasePrivate() => _basePrivate;
    }

    public class DerivedInheritsPolicy : BaseWithVisibilityPolicy
    {
        public int DerivedPublic;
        private int _derivedPrivate;

        public void SetDerived(int p, int priv)
        {
            DerivedPublic = p;
            _derivedPrivate = priv;
        }

        public int GetDerivedPrivate() => _derivedPrivate;
    }

    #endregion

    [Test]
    public async Task Visibility_policy_is_inherited_from_base_type()
    {
        DerivedInheritsPolicy src = new DerivedInheritsPolicy();
        src.SetBase(p: 1, priv: 2);
        src.SetDerived(p: 3, priv: 4);

        DerivedInheritsPolicy clone = src.DeepClone();

        await Assert.That(clone.BasePublic).IsEqualTo(1);
        await Assert.That(clone.GetBasePrivate()).IsEqualTo(0);
        await Assert.That(clone.DerivedPublic).IsEqualTo(3);
        await Assert.That(clone.GetDerivedPrivate()).IsEqualTo(0);
    }

    #region Test types - mixed-accessibility property

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class MixedAccessibilityPropertyDto
    {
        // Property is "public" by C# rules even though the setter is private. The visibility
        // policy must treat it as Public (most permissive of getter/setter).
        public int PublicGetPrivateSet { get; private set; }

        public void Set(int v) => PublicGetPrivateSet = v;
    }

    #endregion

    [Test]
    public async Task PublicOnly_policy_includes_property_with_public_getter_and_private_setter()
    {
        MixedAccessibilityPropertyDto src = new MixedAccessibilityPropertyDto();
        src.Set(42);

        MixedAccessibilityPropertyDto clone = src.DeepClone();

        await Assert.That(clone.PublicGetPrivateSet).IsEqualTo(42);
    }

    #region Test types - [FastClonerIgnore(false)] overrides visibility policy

    [FastClonerVisibility(FastClonerMemberVisibility.Public)]
    public class IgnoreFalseOverridesPolicy
    {
        public int Public;

        // Type-level policy excludes private members; an explicit "don't ignore me"
        // member-level attribute must put it back in (parity with explicit Clone behavior).
        [FastClonerIgnore(false)]
        private int _private;

        public void Set(int p, int priv)
        {
            Public = p;
            _private = priv;
        }

        public int GetPrivate() => _private;
    }

    #endregion

    [Test]
    public async Task FastClonerIgnore_false_overrides_visibility_policy_exclusion()
    {
        IgnoreFalseOverridesPolicy src = new IgnoreFalseOverridesPolicy();
        src.Set(p: 10, priv: 20);

        IgnoreFalseOverridesPolicy clone = src.DeepClone();

        await Assert.That(clone.Public).IsEqualTo(10);
        await Assert.That(clone.GetPrivate()).IsEqualTo(20);
    }
}
