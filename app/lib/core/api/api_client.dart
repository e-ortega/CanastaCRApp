import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class ApiClient {
  static const _baseUrl = '${String.fromEnvironment('API_URL', defaultValue: 'http://localhost:5098')}/api';
  static const _storage = FlutterSecureStorage();

  late final Dio _dio;

  ApiClient() {
    _dio = Dio(BaseOptions(
      baseUrl: _baseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 10),
    ));

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.read(key: 'auth_token');
        if (token != null) options.headers['Authorization'] = 'Bearer $token';
        handler.next(options);
      },
      onError: (error, handler) {
        debugPrint('API error: ${error.requestOptions.path} → ${error.message}');
        handler.next(error);
      },
    ));
  }

  Future<Map<String, dynamic>> get(String path, {Map<String, dynamic>? params}) async {
    final response = await _dio.get(path, queryParameters: params);
    return response.data as Map<String, dynamic>;
  }

  Future<List<dynamic>> getList(String path, {Map<String, dynamic>? params}) async {
    final response = await _dio.get(path, queryParameters: params);
    return response.data as List<dynamic>;
  }

  Future<Map<String, dynamic>> post(String path, Map<String, dynamic> body) async {
    final response = await _dio.post(path, data: body);
    return response.data as Map<String, dynamic>;
  }

  Future<void> patch(String path, [Map<String, dynamic>? body]) async {
    await _dio.patch(path, data: body);
  }

  Future<void> delete(String path) async {
    await _dio.delete(path);
  }

  static Future<void> saveToken(String token) =>
      _storage.write(key: 'auth_token', value: token);

  static Future<void> clearToken() => _storage.delete(key: 'auth_token');

  static Future<String?> getToken() => _storage.read(key: 'auth_token');
}
