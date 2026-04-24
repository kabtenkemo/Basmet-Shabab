# منصة بصمة شباب - Basma Shabab Platform

## 📋 نظرة عامة Overview

منصة إدارة شاملة لتنظيم أعمال المؤسسة والمحافظات ومتابعة الأخبار مع نظام متقدم لإدارة الأعضاء والمهام والشكاوى والمقترحات.

**A comprehensive management platform for organizing institutional operations with advanced member management, task tracking, complaint handling, and suggestion voting system.**

---

## 🚀 الميزات الرئيسية Key Features

### ✅ نظام المستخدمين User System
- تسجيل دخول آمن مع JWT tokens
- 6 مستويات أدوار مختلفة (President, VicePresident, CentralMember, إلخ)
- إدارة صلاحيات متقدمة (RBAC)
- تغيير كلمة السر المجبري عند أول دخول

### ✅ إدارة الأعضاء Member Management
- إنشاء وتعديل وحذف الأعضاء
- تعيين الأدوار والصلاحيات
- نظام نقاط وترتيب المتصدرين
- استخراج تاريخ الميلاد تلقائياً من الرقم القومي المصري

### ✅ إدارة المهام Task Management
- إنشاء المهام وتوزيعها
- تحديد الحالة (مكتملة / قيد التنفيذ)
- توجيه المهام للمناصب أو الأعضاء المحددين

### ✅ نظام الشكاوى Complaint System
- تقديم وتتبع الشكاوى
- نظام التصعيد (3 مستويات)
- الرد الإداري والتعليقات
- سجل تاريخي كامل

### ✅ نظام الأخبار News System
- نشر الأخبار والإعلانات
- توجيه للأدوار أو الأعضاء المحددين
- واجهة سهلة للنشر

### ✅ نظام المقترحات Suggestions System
- تقديم المقترحات والأفكار
- نظام التصويت (قبول / رفض)
- عد الأصوات وتتبع اتجاهات التصويت
- متاح لجميع الأعضاء

### ✅ سجل التدقيق Audit Logs
- تتبع جميع التغييرات
- حفظ القيم القديمة والجديدة
- استعلام متقدم مع التصفية

### ✅ لوحة التحكم Dashboard
- إحصائيات سريعة
- المتصدرون حسب المناصب
- آخر الأخبار والأنشطة
- معلومات مخصصة حسب الدور

---

## 🛠️ التكنولوجيا المستخدمة Tech Stack

### Backend
- **ASP.NET Core 10** Web API
- **Entity Framework Core 10** (ORM)
- **SQL Server** Database
- **JWT Bearer** Authentication
- **BCrypt.Net-Next** Password Hashing
- **Swagger UI** API Documentation

### Frontend
- **React 19** UI Framework
- **Vite** Build Tool
- **TypeScript** Type Safety
- **Tailwind CSS** Dark-only Styling
- **Axios** HTTP Client
- **Context API** State Management
- **React Icons** UI Icons

### Deployment
- **Vercel** Frontend (https://basmet-shabab.vercel.app)
- **runasp.net** Backend (IIS Hosted)
- **SQL Server** hosted database

---

## 📦 التثبيت Installation

### المتطلبات Prerequisites:
```
- .NET 10 SDK
- Node.js 18+
- SQL Server 2019+
- Git
```

### خطوات التثبيت:
```bash
# 1. استنساخ المستودع
git clone https://github.com/yourusername/basma-shabab.git
cd basma-shabab

# 2. تشغيل الخادم الخلفي
cd BasmaApi
dotnet build
dotnet run

# 3. في نافذة جديدة، تشغيل الواجهة الأمامية
cd ../client
npm install
npm run dev
```

---

## 🔐 البيانات الافتراضية Default Credentials

```
البريد الإلكتروني: president@basmet.local
كلمة المرور: 123
الدور: President (رئيس الكيان)
```

⚠️ **تحذير**: غير كلمة السر عند أول دخول!

---

## 🐛 الإصلاحات الأخيرة Recent Fixes

### تاريخ آخر تحديث: 14 أبريل 2026

تم إصلاح جميع مشاكل تسجيل الدخول والجلسات:

✅ منع تغيير البريد الإلكتروني تلقائياً
✅ إيقاف إعادة تشفير كلمة السر في كل بدء
✅ تحسين معالجة الأخطاء والاتصال المقطوع
✅ إضافة رسائل خطأ واضحة
✅ معالجة الأخطاء الجزئية في تحميل البيانات

للتفاصيل الكاملة، راجع [BUGFIXES.md](BUGFIXES.md)

---

## 🧪 الاختبار Testing

### الاختبارات اليدوية:
```bash
# تشغيل الخادم والواجهة كما هو موضح في التثبيت

# ثم افتح:
# Frontend: http://localhost:5173
# Backend API: http://localhost:5000/swagger
```

### حالات الاختبار الرئيسية:
1. تسجيل الدخول بنجاح
2. إنشاء عضو جديد
3. إنشاء مهمة وتوزيعها
4. تقديم ومراجعة شكوى
5. نشر خبر موجه
6. تقديم واختبار المقترحات

للمزيد، راجع [TESTING_GUIDE.md](TESTING_GUIDE.md)

---

## 📊 هيكل قاعدة البيانات Database Schema

```
Members (أعضاء)
├── Id (Guid)
├── Email (string, unique, case-insensitive)
├── PasswordHash (bcrypt)
├── FullName (4 parts required)
├── NationalId (14 digits, Egyptian)
├── BirthDate (auto-extracted)
├── Role (enum: 6 roles)
├── Points (int)
├── MustChangePassword (bool)
└── CreatedAtUtc (datetime)

Tasks (مهام)
├── Id (Guid)
├── Title, Description
├── DueDate
├── Status (Open/Completed)
└── TargetRoles/TargetMembers

Complaints (شكاوى)
├── Id (Guid)
├── Subject, Message
├── Priority (Low/Medium/High)
├── Status (Open/InReview/Resolved/Rejected)
├── EscalationLevel (0-3)
├── ComplaintHistories (audit trail)

Suggestions (مقترحات)
├── Id (Guid)
├── Title, Description
├── Status (Open/Accepted/Rejected)
├── AcceptanceCount, RejectionCount
├── SuggestionVotes (voting records)
```

---

## 🌐 النشر Deployment

### على Vercel (Frontend):
```bash
cd client
npm run build
git push origin main  # Automatic deployment on Vercel
```

### على runasp.net (Backend):
```bash
cd BasmaApi
dotnet publish -c Release
# Upload to runasp.net dashboard
```

---

## 🔍 استكشاف الأخطاء Troubleshooting

### تسجيل الدخول غير ممكن:
```
1. تحقق من قاعدة البيانات: 
   - هل البريد موجود؟
   - هل PasswordHash موجود؟
2. تحقق من السجلات: `dotnet watch run`
3. أعد تعيين بيانات الرئيس في Program.cs
```

### البيانات لا تحفظ:
```
1. تحقق من اتصال قاعدة البيانات
2. تحقق من migrations: `dotnet ef database update`
3. تحقق من الصلاحيات
```

### الواجهة الأمامية لا تعمل:
```
1. تحقق من npm install
2. تحقق من الألوان: npm run dev
3. تحقق من الاتصال بالخادم (F12 DevTools)
```

---

## 📝 المساهمة Contributing

يرحب المشروع بالمساهمات:

1. Fork المستودع
2. إنشاء فرع جديد للميزة (`git checkout -b feature/amazing-feature`)
3. Commit التغييرات (`git commit -m 'Add amazing feature'`)
4. Push للفرع (`git push origin feature/amazing-feature`)
5. فتح Pull Request

---

## 📄 الترخيص License

هذا المشروع مرخص تحت MIT License - انظر [LICENSE](LICENSE) للتفاصيل.

---

## 📞 التواصل Contact

- **البريد الإلكتروني**: support@basma-shabab.local
- **الإصدار**: v1.0.0 (14 أبريل 2026)
- **الحالة**: production-ready ✅

---

## 📚 التوثيق Documentation

- [دليل الاختبار والتطوير](TESTING_GUIDE.md)
- [تقرير الأخطاء والحلول](BUGFIXES.md)
- [Swagger UI](http://localhost:5000/swagger) (development)

---

**تم آخر تحديث**: 14 أبريل 2026
**الحالة**: جميع الأنظمة تعمل بشكل صحيح ✅
