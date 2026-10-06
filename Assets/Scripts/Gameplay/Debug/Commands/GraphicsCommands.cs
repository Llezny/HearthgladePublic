using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hearthglade.Gameplay.Debug.Commands {
    public class GraphicsCommands {

        public void LogGraphicsInfo() {
            UnityEngine.Debug.Log( BuildGraphicsInfo() );
        }

        private static string BuildGraphicsInfo() {
            var sb = new StringBuilder();
            sb.AppendLine( "[GraphicsDiag] ---- graphics info ----" );

            sb.AppendLine( $"[GraphicsDiag] Device: {SystemInfo.deviceModel} ({SystemInfo.graphicsDeviceName})" );
            sb.AppendLine( $"[GraphicsDiag]   graphicsDeviceType: {SystemInfo.graphicsDeviceType}" );
            sb.AppendLine( $"[GraphicsDiag]   graphicsDeviceVersion: {SystemInfo.graphicsDeviceVersion}" );
            sb.AppendLine( $"[GraphicsDiag]   supportsComputeShaders: {SystemInfo.supportsComputeShaders}" );

            int qualityIndex = QualitySettings.GetQualityLevel();
            sb.AppendLine( $"[GraphicsDiag] Quality level: {QualitySettings.names[qualityIndex]} ({qualityIndex})" );

            if ( UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset ) {
                sb.AppendLine( $"[GraphicsDiag] URP asset: {urpAsset.name}" );
                sb.AppendLine( $"[GraphicsDiag]   supportsHDR: {urpAsset.supportsHDR}" );
                sb.AppendLine( $"[GraphicsDiag]   renderScale: {urpAsset.renderScale}" );
                sb.AppendLine( $"[GraphicsDiag]   msaaSampleCount: {urpAsset.msaaSampleCount}" );
            }
            else {
                sb.AppendLine( "[GraphicsDiag] URP asset: none (not using a UniversalRenderPipelineAsset)" );
            }

            var cam = Camera.main;
            if ( cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>( out var camData ) ) {
                sb.AppendLine( $"[GraphicsDiag] Camera: {cam.name}" );
                sb.AppendLine( $"[GraphicsDiag]   renderPostProcessing: {camData.renderPostProcessing}" );
                sb.AppendLine( $"[GraphicsDiag]   allowHDROutput: {camData.allowHDROutput}" );
                sb.AppendLine( $"[GraphicsDiag]   volumeLayerMask: {camData.volumeLayerMask.value}" );

                var stack = VolumeManager.instance.stack;
                var tonemapping = stack.GetComponent<Tonemapping>();
                sb.AppendLine( $"[GraphicsDiag]   Tonemapping active in stack: {tonemapping.IsActive()} (mode: {tonemapping.mode.value})" );
            }
            else {
                sb.AppendLine( "[GraphicsDiag] Camera.main has no UniversalAdditionalCameraData." );
            }

            sb.AppendLine( "[GraphicsDiag] ---- end ----" );
            return sb.ToString();
        }
    }
}
