# دليل الاختبار والتطوير - Testing & Development Guide

## 🚀 كيفية الاختبار المحلي - How to Test Locally

### المتطلبات Prerequisites:
- .NET 10 SDK
- Node.js 18+
- SQL Server (LocalDB or full edition)
- Git

### خطوات البدء - Startup Steps:

#### 1. نسخ المستودع Clone Repository:
```bash
cd e:\basma
git pull origin main
```

#### 2. تشغيل الخادم المحلي Run Backend:
```bash
cd BasmaApi
dotnet build
dotnet watch run
```

الخادم سيبدأ على: `http://localhost:5000` أو `https://localhost:5001`

#### 3. تشغيل الواجهة المحلية Run Frontend:
```bash
cd client
npm install
npm run dev
```

الواجهة ستفتح على: `http://localhost:5173`

---

## 🧪 حالات الاختبار - Test Cases

### ✅ اختبار تسجيل الدخول - Login Test:
```
البريد الإلكتروني: president@basmet.local
كلمة المرور: 123
النتيجة المتوقعة: تسجيل دخول ناجح، إعادة توجيه للوحة التحكم
```

### ✅ اختبار الجلسة المنقطعة - Disconnected Session:
1. سجل الدخول بنجاح
2. افصل الإنترنت (أو استخدم DevTools)
3. حاول تحديث البيانات
4. النتيجة المتوقعة: رسالة خطأ واضحة

### ✅ اختبار استقرار البريد - Email Stability Test:
1. اقرأ بريد الرئيس من قاعدة البيانات
2. أعد تشغيل الخادم 5 مرات
3. تحقق من أن البريد لم يتغير

### ✅ اختبار حفظ البيانات - Data Persistence:
1. أنشئ عضواً جديداً
2. أغلق المتصفح
3. أعد تسجيل الدخول
4. تحقق من وجود العضو الجديد

---

## 🔍 التحقق من السجلات - Checking Logs

### السجلات الخلفية - Backend Logs:
```bash
# في نافذة PowerShell حيث يعمل `dotnet watch run`
# ابحث عن:
# ✅ Created new President account
# ℹ️ President account already configured correctly - skipping update
# 🔄 President email updated
```

### السجلات الأمامية - Frontend Logs:
```javascript
// في DevTools Console (F12)
// ابحث عن:
// ✅ Login successful
// ❌ Session load failed: ...
// ⚠️ Failed to load dashboard: ...
```

---

## 📊 مراقبة قاعدة البيانات - Database Monitoring

### فتح SQL Server Management Studio:
```sql
-- تحقق من حساب الرئيس:
SELECT Id, Email, FullName, MustChangePassword, PasswordHash 
FROM Members 
WHERE Role = 0 -- 0 = President
```

### تتبع آخر تغيير:
```sql
-- تحقق من أن البريد لم يتغير:
SELECT TOP 10 * FROM AuditLogs 
WHERE EntityName = 'User' AND ActionType = 'Update'
ORDER BY TimestampUtc DESC
```

---

## 🚀 نشر الإصلاحات - Deployment

### على Vercel (Frontend):
```bash
cd client
npm run build
# سيتم النشر تلقائياً من git
```

### على runasp.net (Backend):
```bash
cd BasmaApi
dotnet publish -c Release
# ثم upload البجات من `bin/Release/net10.0/publish/`
```

---

## 🔐 متغيرات البيئة - Environment Variables

### Development (.env):
```env
VITE_API_BASE_URL=http://localhost:5000
```

### Production (Vercel settings):
```env
VITE_API_BASE_URL=https://basmet-shabab.runasp.net
```

---

## 📋 Checklist للنشر - Pre-Deployment Checklist

قبل نشر الإصلاحات، تحقق من:

- [ ] جميع الاختبارات المحلية تمر ✅
- [ ] لا توجد أخطاء في Console ✅
- [ ] تسجيل الدخول يعمل بشكل صحيح ✅
- [ ] البيانات تحفظ بشكل صحيح ✅
- [ ] الأخطاء تعرض رسائل واضحة ✅
- [ ] Database migrations تعمل ✅
- [ ] CORS configured correctly ✅
- [ ] JWT tokens configured ✅
- [ ] Logging enabled for debugging ✅

---

## 🆘 استكشاف الأخطاء - Troubleshooting

### مشكلة: "Cannot connect to database"
```
الحل Solution:
1. تحقق من اتصال SQL Server
2. جرب: `(localdb)\mssqllocaldb`
3. استخدم SQL Server Management Studio للتحقق
```

### مشكلة: Login fails with "unauthorized"
```
الحل Solution:
1. تحقق من password hash في قاعدة البيانات
2. جرب إعادة تعيين البيان:
   UPDATE Members SET PasswordHash = NULL WHERE Role = 0
   (ثم أعد تشغيل التطبيق)
```

### مشكلة: Frontend build fails
```
الحل Solution:
npm install
npm run build -- --debug
```

---

## 📞 للمزيد من المساعدة - Support

- Backend Issues: تحقق من `Program.cs` startup logs
- Frontend Issues: افتح DevTools (F12)
- Database Issues: استخدم SQL Server Management Studio
- API Issues: اختبر باستخدام Swagger UI على `http://localhost:5000/swagger`

---

**آخر تحديث**: 14 أبريل 2026
