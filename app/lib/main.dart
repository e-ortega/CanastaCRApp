import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'core/api/api_client.dart';
import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/providers/auth_provider.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const CanastaCRApp());
}

class CanastaCRApp extends StatelessWidget {
  const CanastaCRApp({super.key});

  @override
  Widget build(BuildContext context) {
    final api = ApiClient();
    final auth = AuthProvider(api);
    auth.checkAuth();

    return ChangeNotifierProvider.value(
      value: auth,
      child: Builder(
        builder: (context) {
          final router = buildRouter(auth);
          return MaterialApp.router(
            title: 'CanastaCR',
            theme: AppTheme.light,
            routerConfig: router,
            debugShowCheckedModeBanner: false,
          );
        },
      ),
    );
  }
}
