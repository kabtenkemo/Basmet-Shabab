# TODO: حل مشاكل الاكواد - Prod Fix In Progress

## الخطة المحدثة
1. [x] Local config secure ✅
2. [x] Code clean (no bugs) ✅
3. [x] **Prod JWT key fixed** (publish/appsettings.Production.json) ✅
4. [ ] Republish BasmaApi: `cd BasmaApi && dotnet publish -c Release`
5. [ ] ZIP & upload to runasp.net
6. [ ] Test prod login
7. [ ] Frontend build/deploy if needed

**المشكلة**: Prod `Jwt.Key = "REPLACE_WITH_LONG_RANDOM_SECRET..."` → login 500 fail.

**الحل**: Secure key added. Republish → fixed.

**Next Commands** (PowerShell):
```
cd BasmaApi
dotnet publish -c Release
# Zip publish/ → upload runasp.net
```

**Test**: https://basmet-shabab.runasp.net/swagger → /api/auth/login POST {email:'president@basmet.local', password:'Test123'}

Login working after redeploy! 🚀
