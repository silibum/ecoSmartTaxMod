using System;
using System.Linq;
using System.Reflection;
using System.Collections;

namespace Eco.Mods.SmartTax
{
    using Core.Systems;
    using Gameplay.Players;

    public static class SmartTaxCompany
    {
        private static readonly Assembly CompaniesMod;
        private static readonly Type CompanyClass;
        private static readonly MethodInfo AllRegistrars;
        private static readonly PropertyInfo CompanyAllEmployess;
        private static readonly PropertyInfo CompanyLegalPerson;

        static SmartTaxCompany()
        {
            CompaniesMod = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "EcoCompaniesMod");
            CompanyClass = CompaniesMod.GetTypes().First(t => t.Name == "Company");
            AllRegistrars = typeof(Registrars).GetMethod(nameof(Registrars.All), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(CompanyClass);

            CompanyAllEmployess = CompanyClass.GetProperty("AllEmployees", BindingFlags.Public | BindingFlags.Instance);
            CompanyLegalPerson = CompanyClass.GetProperty("LegalPerson", BindingFlags.Public | BindingFlags.Instance);
        }

        public static User GetLegalPersonForEmployee(User user)
        {
            var companies = (IEnumerable)AllRegistrars.Invoke(null, null)!;

            foreach (var comp in companies)
            {
                var employees = (IEnumerable)CompanyAllEmployess.GetValue(comp)!;
                var containsMtd = employees.GetType().GetMethod("Contains", [ user.GetType() ]);

                bool isMember = containsMtd != null ? (bool)containsMtd.Invoke(employees, new[] { user }!)! : employees.Cast<object>().Any(e => e.Equals(user));

                if (isMember)
                {
                    return CompanyLegalPerson.GetValue(comp) as User;
                }
            }

            return null;
        }
    }
}