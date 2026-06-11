import 'package:flutter/foundation.dart';
import '../../../core/api/api_client.dart';

class AuthProvider extends ChangeNotifier {
  final ApiClient _api;

  String? _userId;
  String? _displayName;
  bool _loading = false;
  String? _error;

  AuthProvider(this._api);

  bool get isAuthenticated => _userId != null;
  String? get displayName => _displayName;
  bool get loading => _loading;
  String? get error => _error;

  Future<void> checkAuth() async {
    final token = await ApiClient.getToken();
    if (token != null) {
      _userId = 'cached';
      notifyListeners();
    }
  }

  Future<bool> login(String email, String password) async {
    _loading = true;
    _error = null;
    notifyListeners();

    try {
      final result = await _api.post('/auth/login', {'email': email, 'password': password});
      await ApiClient.saveToken(result['token']);
      _userId = result['userId'];
      _displayName = result['displayName'];
      return true;
    } catch (_) {
      _error = 'Email o contraseña incorrectos.';
      return false;
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<bool> register(String email, String displayName, String password) async {
    _loading = true;
    _error = null;
    notifyListeners();

    try {
      final result = await _api.post('/auth/register', {
        'email': email,
        'displayName': displayName,
        'password': password,
      });
      await ApiClient.saveToken(result['token']);
      _userId = result['userId'];
      _displayName = result['displayName'];
      return true;
    } catch (_) {
      _error = 'No se pudo crear la cuenta. El email puede estar en uso.';
      return false;
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    await ApiClient.clearToken();
    _userId = null;
    _displayName = null;
    notifyListeners();
  }
}
